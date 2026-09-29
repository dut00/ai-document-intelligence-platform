using System.Net;
using System.Net.Http.Json;
using System.Text;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DocumentIntelligence.IntegrationTests;

public sealed class RateLimitingTests(ApiFactory factory)
{
    private static readonly object _wrongCredentials = new { email = "nobody@example.com", password = "WrongPassword1" };

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Auth_requests_are_limited_per_client_ip_address()
    {
        using var client = ClientFrom("203.0.113.10");

        for (var attempt = 0; attempt < ApiFactory.AuthPermitLimit; attempt++)
        {
            (await LoginAsync(client)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var limited = await LoginAsync(client);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldNotBeNull().ShouldBeGreaterThan(TimeSpan.Zero);
        var problem = await limited.Content.ReadFromJsonAsync<ProblemDetails>(CancellationToken);
        problem.ShouldNotBeNull().Status.ShouldBe(StatusCodes.Status429TooManyRequests);

        using var otherClient = ClientFrom("203.0.113.11");
        (await LoginAsync(otherClient)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Uploads_are_limited_per_user()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var content = Encoding.UTF8.GetBytes("Meeting notes");

        for (var upload = 0; upload < ApiFactory.UploadTokenLimit; upload++)
        {
            (await client.UploadDocumentAsync($"notes-{upload}.txt", "text/plain", content)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
        }

        (await client.UploadDocumentAsync("one-too-many.txt", "text/plain", content)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await client.GetAsync<DocumentStatsResponse>("/api/documents/stats")).Total.ShouldBe(ApiFactory.UploadTokenLimit);

        var (otherClient, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        (await otherClient.UploadDocumentAsync("notes.txt", "text/plain", content)).StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    private HttpClient ClientFrom(string ipAddress)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, ipAddress);

        return client;
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client) =>
        client.PostAsJsonAsync("/api/auth/login", _wrongCredentials, CancellationToken);
}
