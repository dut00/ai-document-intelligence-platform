extern alias worker;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using DocumentIntelligence.Api.Endpoints;
using DocumentIntelligence.Application.Authentication;
using DocumentIntelligence.Domain.Users;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Testcontainers.PostgreSql;
using worker::DocumentIntelligence.Worker.Consumers;

[assembly: AssemblyFixture(typeof(DocumentIntelligence.IntegrationTests.Infrastructure.ApiFactory))]

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the API in memory against throwaway PostgreSQL and SeaweedFS containers, shared by all tests
/// in the assembly. RabbitMQ is replaced by MassTransit's in-memory test harness, which also hosts the
/// Worker's consumers. Migrations are applied on startup because the host runs in the Development environment.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "Password123";

    private const int S3Port = 8333;
    private const string S3AccessKey = "test-access-key";
    private const string S3SecretKey = "test-secret-key";

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

        builder.ConfigureTestServices(services => services.AddMassTransitTestHarness(bus =>
        {
            bus.AddConsumer<DocumentDeletedConsumer>();

            // Outbox delivery polls in the background, so allow more than the default second of bus inactivity.
            bus.SetTestTimeouts(testTimeout: TimeSpan.FromSeconds(30), testInactivityTimeout: TimeSpan.FromSeconds(10));
        }));
    }
}
