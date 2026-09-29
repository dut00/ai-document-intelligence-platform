using Amazon.Runtime;
using Amazon.S3;
using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Data;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Infrastructure.Authentication;
using DocumentIntelligence.Infrastructure.DomainEvents;
using DocumentIntelligence.Infrastructure.Identity;
using DocumentIntelligence.Infrastructure.Messaging;
using DocumentIntelligence.Infrastructure.Persistence;
using DocumentIntelligence.Infrastructure.Persistence.Repositories;
using DocumentIntelligence.Infrastructure.Storage;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DocumentIntelligence.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "Postgres";
    public const string MessageBrokerConnectionName = "RabbitMq";

    /// <summary>
    /// Adapters shared by the API and the Worker: persistence, object storage and messaging.
    /// Each host registers its own consumers through <paramref name="configureConsumers"/>.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddPersistence(configuration);
        services.AddStorage();
        services.AddMessaging(configuration, configureConsumers);

        return services;
    }

    /// <summary>
    /// User accounts and JWT bearer authentication; needed only by the API.
    /// </summary>
    public static IServiceCollection AddIdentityAndTokens(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IUserAccountService, IdentityUserAccountService>();
        services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

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
    }

    private static void AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers)
    {
        var connectionString = configuration.GetConnectionString(MessageBrokerConnectionName)
            ?? throw new InvalidOperationException($"Connection string '{MessageBrokerConnectionName}' is not configured.");

        services.AddScoped<IIntegrationEventPublisher, MassTransitIntegrationEventPublisher>();

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

            configureConsumers?.Invoke(bus);

            bus.UsingRabbitMq((context, rabbit) =>
            {
                rabbit.Host(new Uri(connectionString));
                rabbit.ConfigureEndpoints(context);
            });
        });
    }
}
