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
    public async Task Auth_requests_through_a_trusted_proxy_are_limited_per_forwarded_client_ip_address()
    {
        using var client = ClientFrom(ApiFactory.TrustedProxyIp);

        for (var attempt = 0; attempt < ApiFactory.AuthPermitLimit; attempt++)
        {
            (await LoginAsync(client, forwardedFor: "203.0.113.20")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await LoginAsync(client, forwardedFor: "203.0.113.20")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        // Another client behind the same proxy has its own limit.
        (await LoginAsync(client, forwardedFor: "203.0.113.21")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forwarded_client_ip_address_from_an_untrusted_sender_is_ignored()
    {
        using var client = ClientFrom("203.0.113.30");

        for (var attempt = 0; attempt < ApiFactory.AuthPermitLimit; attempt++)
        {
            (await LoginAsync(client, forwardedFor: $"203.0.113.{100 + attempt}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        (await LoginAsync(client, forwardedFor: "203.0.113.200")).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task Registrations_are_limited_per_client_ip_address()
    {
        using var client = ClientFrom("203.0.113.40");

        for (var account = 0; account < ApiFactory.RegisterPermitLimit; account++)
        {
            (await RegisterAsync(client)).StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        (await RegisterAsync(client)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using var otherClient = ClientFrom("203.0.113.41");
        (await RegisterAsync(otherClient)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Failed_logins_are_throttled_per_account_and_client_ip_address_without_locking_the_account()
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        (await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = ApiFactory.Password }, CancellationToken))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        using var attacker = ClientFrom("203.0.113.50");
        for (var attempt = 0; attempt < ApiFactory.LoginAttemptsPerAccount; attempt++)
        {
            (await LoginAsync(attacker, email: email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // Refused even with the right password: the throttle answers before the password is checked.
        var refused = await LoginAsync(attacker, email: email, password: ApiFactory.Password);
        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        refused.Headers.RetryAfter.ShouldNotBeNull().Delta.ShouldNotBeNull().ShouldBeGreaterThan(TimeSpan.Zero);

        // The owner, from another address, still signs in.
        using var owner = ClientFrom("203.0.113.51");
        (await LoginAsync(owner, email: email, password: ApiFactory.Password)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ipv6_addresses_of_one_64_bit_prefix_share_the_auth_limit()
    {
        using var client = ClientFrom("2001:db8:aa:1::1");
        for (var attempt = 0; attempt < ApiFactory.AuthPermitLimit; attempt++)
        {
            (await LoginAsync(client)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // One subscriber usually holds the whole /64: a new address in it gets no new budget.
        using var samePrefix = ClientFrom("2001:db8:aa:1::2");
        (await LoginAsync(samePrefix)).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);

        using var otherPrefix = ClientFrom("2001:db8:aa:2::1");
        (await LoginAsync(otherPrefix)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unicode_look_alikes_of_an_email_share_its_login_attempts()
    {
        var email = $"kate-{Guid.NewGuid():N}@example.com";
        using var attacker = ClientFrom("203.0.113.60");

        for (var attempt = 0; attempt < ApiFactory.LoginAttemptsPerAccount; attempt++)
        {
            (await LoginAsync(attacker, email: email)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        // U+212A KELVIN SIGN normalizes to "K", the way Identity looks the account up.
        (await LoginAsync(attacker, email: email.Replace('k', 'K'))).StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(300)]
    public async Task Login_without_a_plausible_email_is_a_validation_problem(int? emailLength)
    {
        using var client = ClientFrom("203.0.113.70");
        object credentials = emailLength is { } length
            ? new { email = new string('a', length) + "@example.com", password = "WrongPassword1" }
            : new { password = "WrongPassword1" };

        var response = await client.PostAsJsonAsync("/api/auth/login", credentials, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client) =>
        client.PostAsJsonAsync(
            "/api/auth/register", new { email = $"user-{Guid.NewGuid():N}@example.com", password = ApiFactory.Password }, CancellationToken);

    // A different unknown email per attempt unless one is given, so only the per-IP limit applies.
    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string? forwardedFor = null, string? email = null, string password = "WrongPassword1")
    {
        var credentials = new { email = email ?? $"nobody-{Guid.NewGuid():N}@example.com", password };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(credentials) };
        if (forwardedFor is not null)
        {
            request.Headers.Add("X-Forwarded-For", forwardedFor);
        }

        return await client.SendAsync(request, CancellationToken);
    }
}
