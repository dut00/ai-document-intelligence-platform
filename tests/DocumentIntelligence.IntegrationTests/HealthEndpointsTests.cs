using System.Net;
using DocumentIntelligence.IntegrationTests.Infrastructure;

namespace DocumentIntelligence.IntegrationTests;

public sealed class HealthEndpointsTests(ApiFactory factory)
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Health_endpoint_returns_ok(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
