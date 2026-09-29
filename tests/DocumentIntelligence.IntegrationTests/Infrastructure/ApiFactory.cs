using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(DocumentIntelligence.IntegrationTests.Infrastructure.ApiFactory))]

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the API in memory against a throwaway PostgreSQL container, shared by all tests in the assembly.
/// Migrations are applied on startup because the host runs in the Development environment.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-at-least-32-characters");
        builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", string.Empty);
    }
}
