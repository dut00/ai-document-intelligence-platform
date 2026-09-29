using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DocumentIntelligence.IntegrationTests.Infrastructure;

namespace DocumentIntelligence.IntegrationTests;

public sealed class HealthEndpointsTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoint_returns_ok(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_checks_the_database_the_storage_and_the_message_bus()
    {
        var checks = await GetChecksAsync("/health/ready");

        checks.ShouldContainKeyAndValue("postgres", "Healthy");
        checks.ShouldContainKeyAndValue("storage", "Healthy");
        checks.Keys.ShouldContain(name => name.StartsWith("masstransit", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Liveness_does_not_depend_on_external_services()
    {
        var checks = await GetChecksAsync("/health/live");

        checks.Keys.ShouldBe(["self"]);
    }

    private async Task<Dictionary<string, string?>> GetChecksAsync(string path)
    {
        using var client = factory.CreateClient();

        var report = await client.GetFromJsonAsync<JsonElement>(path, CancellationToken);

        return report.GetProperty("checks").EnumerateArray().ToDictionary(
            check => check.GetProperty("name").GetString()!,
            check => check.GetProperty("status").GetString());
    }
}
