extern alias worker;

using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using DocumentIntelligence.Api.Endpoints;
using DocumentIntelligence.Api.Realtime;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Application.Abstractions.Calendar;
using DocumentIntelligence.Application.Authentication;
using DocumentIntelligence.Domain.Users;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using worker::DocumentIntelligence.Worker.Consumers;

[assembly: AssemblyFixture(typeof(DocumentIntelligence.IntegrationTests.Infrastructure.ApiFactory))]

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the API in memory against throwaway PostgreSQL and SeaweedFS containers, shared by all tests
/// in the assembly. RabbitMQ is replaced by MassTransit's in-memory test harness, which also hosts the
/// Worker's consumers. Migrations are applied on startup because the host runs in the Development environment.
/// Documents are analyzed by the fake analyzer, and public holidays come from <see cref="StubPublicHolidayProvider"/>.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Password123";

    /// <summary>
    /// Retries per message in tests: a failing message faults after this many attempts plus one.
    /// </summary>
    public const int MessageRetryLimit = 2;

    /// <summary>
    /// Auth requests per client IP address, and uploads per user, before the API answers 429.
    /// Requests get a random client IP address unless they pin one (<see cref="TestClientIpStartupFilter"/>).
    /// </summary>
    public const int AuthPermitLimit = 5;

    /// <inheritdoc cref="AuthPermitLimit"/>
    public const int UploadTokenLimit = 5;

    /// <summary>
    /// Accounts one client IP address may create per hour.
    /// </summary>
    public const int RegisterPermitLimit = 3;

    /// <summary>
    /// Failed logins for one account from one client IP address before that address is refused.
    /// Below <see cref="AuthPermitLimit"/>, so the throttle is reached before the per-IP limit.
    /// </summary>
    public const int LoginAttemptsPerAccount = 3;

    /// <summary>
    /// An address whose X-Forwarded-For header the API believes, as it would a reverse proxy's. It is
    /// trusted as part of a network, the way the Docker "full" profile trusts the compose network.
    /// </summary>
    public const string TrustedProxyIp = "198.51.100.1";

    private const string TrustedProxyNetwork = "198.51.100.0/24";

    private const int S3Port = 8333;
    private const string S3AccessKey = "test-access-key";
    private const string S3SecretKey = "test-secret-key";

    private static readonly TimeSpan _messageTimeout = TimeSpan.FromSeconds(15);

    private static readonly string _s3Config = $$"""
        {
          "identities": [
            {
              "name": "test",
              "credentials": [{ "accessKey": "{{S3AccessKey}}", "secretKey": "{{S3SecretKey}}" }],
              "actions": ["Admin", "Read", "Write", "List", "Tagging"]
            }
          ]
        }
        """;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    private readonly IContainer _storage = new ContainerBuilder("chrislusf/seaweedfs:4.47")
        .WithCommand("server", "-dir=/data", "-s3", $"-s3.port={S3Port}", "-s3.config=/etc/seaweedfs/s3.json")
        .WithResourceMapping(Encoding.UTF8.GetBytes(_s3Config), "/etc/seaweedfs/s3.json")
        .WithPortBinding(S3Port, assignRandomHostPort: true)
        // Any HTTP answer (403 for an anonymous request) means the S3 gateway is serving.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
            .ForPort(S3Port)
            .ForStatusCodeMatching(_ => true)))
        .Build();

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _storage.StartAsync());

        // Start the host (and migrate) once here; test classes run in parallel and would race to do it.
        _ = Server;
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        await _storage.DisposeAsync();
    }

    /// <summary>
    /// A client authenticated as a newly registered user.
    /// </summary>
    public async Task<(HttpClient Client, UserId UserId)> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        var client = CreateClient();
        var credentials = new { email = $"user-{Guid.NewGuid():N}@example.com", password = Password };

        var register = await client.PostAsJsonAsync("/api/auth/register", credentials, cancellationToken);
        register.EnsureSuccessStatusCode();
        var user = await register.Content.ReadFromJsonAsync<UserResponse>(cancellationToken);

        var login = await client.PostAsJsonAsync("/api/auth/login", credentials, cancellationToken);
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);

        return (client, new UserId(user!.Id));
    }

    /// <summary>
    /// A connection to <see cref="DocumentsHub"/> the way a browser makes it: straight over a WebSocket,
    /// with the access token in the query string. Not started yet.
    /// </summary>
    public HubConnection CreateHubConnection(string? accessToken) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, DocumentsHub.Path), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, cancellationToken) =>
                {
                    var uri = accessToken is null
                        ? context.Uri
                        : new Uri($"{context.Uri}{(context.Uri.Query.Length == 0 ? '?' : '&')}access_token={Uri.EscapeDataString(accessToken)}");

                    return await Server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
                };
            })
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .Build();

    /// <summary>
    /// Waits until a matching message is published, or gives up after <see cref="_messageTimeout"/>.
    /// </summary>
    public Task<bool> PublishedAsync<T>(Func<T, bool> filter)
        where T : class =>
        EventuallyAsync(cancellationToken =>
            Services.GetTestHarness().Published.Any<T>(context => filter(context.Context.Message), cancellationToken));

    /// <summary>
    /// Waits until a matching message is consumed, or gives up after <see cref="_messageTimeout"/>.
    /// </summary>
    public Task<bool> ConsumedAsync<T>(Func<T, bool> filter)
        where T : class =>
        EventuallyAsync(cancellationToken =>
            Services.GetTestHarness().Consumed.Any<T>(context => filter(context.Context.Message), cancellationToken));

    // The harness stops waiting once the bus looks idle, but messages a consumer's outbox delivers after
    // its commit do not count as bus activity, so a single check can miss them.
    private static async Task<bool> EventuallyAsync(Func<CancellationToken, Task<bool>> check)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var started = Stopwatch.GetTimestamp();

        while (!await check(cancellationToken))
        {
            if (Stopwatch.GetElapsedTime(started) > _messageTimeout)
            {
                return false;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        return true;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("ConnectionStrings:RabbitMq", "amqp://unused");
        builder.UseSetting("Storage:ServiceUrl", $"http://{_storage.Hostname}:{_storage.GetMappedPublicPort(S3Port)}");
        builder.UseSetting("Storage:AccessKey", S3AccessKey);
        builder.UseSetting("Storage:SecretKey", S3SecretKey);
        builder.UseSetting("Storage:BucketName", "documents");
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-characters");
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);

        // Never call Claude from tests, even when the machine has an API key configured.
        builder.UseSetting("Anthropic:ApiKey", string.Empty);
        builder.UseSetting("ANTHROPIC_API_KEY", string.Empty);

        builder.UseSetting("Messaging:Retry:RetryLimit", MessageRetryLimit.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("Messaging:Retry:MinInterval", "00:00:00.050");
        builder.UseSetting("Messaging:Retry:MaxInterval", "00:00:00.200");
        builder.UseSetting("Messaging:Retry:IntervalDelta", "00:00:00.050");

        builder.UseSetting("RateLimiting:AuthPermitLimit", AuthPermitLimit.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("RateLimiting:UploadTokenLimit", UploadTokenLimit.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("RateLimiting:RegisterPermitLimit", RegisterPermitLimit.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("RateLimiting:LoginAttemptsPerAccount", LoginAttemptsPerAccount.ToString(CultureInfo.InvariantCulture));
        builder.UseSetting("RateLimiting:UploadTokensPerPeriod", "1");
        builder.UseSetting("RateLimiting:UploadReplenishmentPeriod", "01:00:00");
        builder.UseSetting("ForwardedHeaders:KnownNetworks:0", TrustedProxyNetwork);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>();
            services.AddSingleton<IPublicHolidayProvider, StubPublicHolidayProvider>();
            services.Decorate<IDocumentAnalyzer, TransientFailureAnalyzer>();

            services.AddMassTransitTestHarness(bus =>
            {
                bus.AddWorkerConsumers();
                bus.AddConsumer<DocumentStatusChangedProbe>();

                // Outbox delivery polls in the background, so allow more than the default second of bus inactivity.
                bus.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(30), testInactivityTimeout: TimeSpan.FromSeconds(10));
            });
        });
    }
}
