using Amazon.Runtime;
using Amazon.S3;
using Anthropic;
using Anthropic.Core;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Calendar;
using DocumentIntelligence.Application.Abstractions.Data;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Infrastructure.Analysis;
using DocumentIntelligence.Infrastructure.Authentication;
using DocumentIntelligence.Infrastructure.Calendar;
using DocumentIntelligence.Infrastructure.DomainEvents;
using DocumentIntelligence.Infrastructure.Identity;
using DocumentIntelligence.Infrastructure.Messaging;
using DocumentIntelligence.Infrastructure.Persistence;
using DocumentIntelligence.Infrastructure.Persistence.Repositories;
using DocumentIntelligence.Infrastructure.Storage;
using DocumentIntelligence.Infrastructure.TextExtraction;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocumentIntelligence.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "Postgres";
    public const string MessageBrokerConnectionName = "RabbitMq";

    /// <summary>
    /// Tag of the health checks of external dependencies (the message broker's check has it too).
    /// </summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Adapters for every Application port, shared by the API and the Worker.
    /// Each host registers its own consumers through <paramref name="configureConsumers"/>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<DocumentLimitsOptions>()
            .BindConfiguration(DocumentLimitsOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddPersistence(configuration);
        services.AddUserAccounts();
        services.AddStorage();
        services.AddDocumentAnalysis(configuration);
        services.AddHolidayCalendar();
        services.AddMessaging(configuration, configureConsumers);

        return services;
    }

    /// <summary>
    /// JWT bearer authentication of incoming requests. Needed only by the API, which therefore
    /// also validates the token settings on startup.
    /// </summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .Validate<IHostEnvironment>(
                (options, environment) => environment.IsDevelopment() || !JwtOptions.PublishedSigningKeys.Contains(options.SigningKey),
                "Jwt:SigningKey is a key published in the repository; set a secret one (e.g. openssl rand -base64 48).")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Keep the raw JWT claim names ("sub", "email") instead of the legacy SOAP claim URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = JwtAccessTokenIssuer.CreateSigningKey(jwt.SigningKey),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = "email",
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        return services;
    }

    private static void AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(DatabaseConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{DatabaseConnectionName}' is not configured.");

        services.AddScoped<DomainEventDispatcher>();
        services.AddScoped<DispatchDomainEventsInterceptor>();

        services.AddDbContext<ApplicationDbContext>((provider, options) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(provider.GetRequiredService<DispatchDomainEventsInterceptor>()));

        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IReadDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddScoped<IAnalysisUsageLog, AnalysisUsageLog>();

        services.AddHealthChecks().AddDbContextCheck<ApplicationDbContext>("postgres", tags: [ReadyTag]);
    }

    // The Worker never resolves these ports, so it needs no JWT settings: the options are
    // validated when first used, or on startup by AddJwtAuthentication.
    private static void AddUserAccounts(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = false;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations();

        services.AddScoped<IUserAccountService, IdentityUserAccountService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
    }

    private static void AddStorage(this IServiceCollection services)
    {
        services.AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAmazonS3>(provider =>
        {
            var storage = provider.GetRequiredService<IOptions<StorageOptions>>().Value;

            return new AmazonS3Client(
                new BasicAWSCredentials(storage.AccessKey, storage.SecretKey),
                new AmazonS3Config
                {
                    ServiceURL = storage.ServiceUrl.ToString(),
                    // S3-compatible stores address buckets by path, not by subdomain.
                    ForcePathStyle = true,
                    // The SDK's default trailing checksums are not supported by every S3-compatible store.
                    RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                    ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                });
        });

        services.AddSingleton<IFileStorage, S3FileStorage>();
        services.AddHostedService<StorageBucketInitializer>();

        services.AddHealthChecks().AddCheck<StorageHealthCheck>("storage", tags: [ReadyTag], timeout: TimeSpan.FromSeconds(5));
    }

    private static void AddDocumentAnalysis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ITextExtractor, PdfTextExtractor>();
        services.AddSingleton<ITextExtractor, PlainTextExtractor>();

        var section = configuration.GetSection(AnthropicOptions.SectionName);
        var apiKey = section[nameof(AnthropicOptions.ApiKey)] is { Length: > 0 } configuredKey
            ? configuredKey
            : configuration[AnthropicOptions.ApiKeyVariable];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<IDocumentAnalyzer, FakeDocumentAnalyzer>();
            return;
        }

        services.AddOptions<AnthropicOptions>()
            .Bind(section)
            .Configure(options => options.ApiKey = apiKey)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IAnthropicClient>(provider =>
        {
            var anthropic = provider.GetRequiredService<IOptions<AnthropicOptions>>().Value;

            return new AnthropicClient(new ClientOptions
            {
                ApiKey = anthropic.ApiKey,
                MaxRetries = anthropic.MaxRetries,
                Timeout = anthropic.Timeout,
            });
        });

        services.AddScoped<IDocumentAnalyzer, ClaudeDocumentAnalyzer>();
    }

    private static void AddHolidayCalendar(this IServiceCollection services)
    {
        services.AddOptions<HolidaysOptions>()
            .BindConfiguration(HolidaysOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMemoryCache();

        services
            .AddHttpClient<IPublicHolidayProvider, NagerDateHolidayProvider>((provider, client) =>
                client.BaseAddress = provider.GetRequiredService<IOptions<HolidaysOptions>>().Value.BaseUrl)
            .AddStandardResilienceHandler(resilience =>
            {
                // Fail fast: without the calendar the dates are still saved, just without the check.
                resilience.Retry.MaxRetryAttempts = 2;
                resilience.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
            });
    }

    private static void AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers)
    {
        var connectionString = configuration.GetConnectionString(MessageBrokerConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{MessageBrokerConnectionName}' is not configured.");

        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

        services.AddOptions<MessageRetryOptions>()
            .BindConfiguration(MessageRetryOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            // Messages published outside a consumer (e.g. from an API request) are stored in the
            // outbox tables by SaveChanges and delivered to RabbitMQ by a background service.
            bus.AddEntityFrameworkOutbox<ApplicationDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.UseBusOutbox();
            });

            // Every consumer endpoint: retry outside, the transactional inbox/outbox inside. Each attempt
            // runs in its own transaction, and messages it publishes are sent only once it commits.
            bus.AddConfigureEndpointsCallback((context, name, endpoint) =>
            {
                if (NotificationEndpoints.IsNotificationEndpoint(name))
                {
                    endpoint.DiscardFaultedMessages();
                    return;
                }

                // Quorum queues count deliveries that were never acknowledged (a crashed consumer): the Worker
                // hands a redelivered document to its isolated endpoint, which fails it once it keeps
                // killing the Worker on its own (DocumentUploadedConsumer, IsolatedDocumentConsumer).
                if (endpoint is IRabbitMqReceiveEndpointConfigurator rabbit)
                {
                    rabbit.SetQuorumQueue();
                }

                var retry = context.GetRequiredService<IOptions<MessageRetryOptions>>().Value;

                endpoint.UseMessageRetry(policy =>
                {
                    policy.Exponential(retry.RetryLimit, retry.MinInterval, retry.MaxInterval, retry.IntervalDelta);

                    // A broken invariant is a bug, not a glitch: another attempt would fail the same way.
                    policy.Ignore<DomainException>();
                });
                endpoint.UseEntityFrameworkOutbox<ApplicationDbContext>(context);
            });

            configureConsumers?.Invoke(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(connectionString));
                rabbit.ConfigureEndpoints(context);
            });
        });
    }
}
