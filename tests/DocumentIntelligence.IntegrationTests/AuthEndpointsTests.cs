using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using DocumentIntelligence.Api.Endpoints;
using DocumentIntelligence.Application.Authentication;
using DocumentIntelligence.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DocumentIntelligence.IntegrationTests;

public sealed class AuthEndpointsTests(ApiFactory factory)
{
    private const string Password = "Password123";

    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Register_login_me_refresh_logout_flow()
    {
        var email = UniqueEmail();

        var register = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password }, CancellationToken);
        register.StatusCode.ShouldBe(HttpStatusCode.Created);
        var registered = await register.Content.ReadFromJsonAsync<UserResponse>(CancellationToken);

        var (accessToken, refreshToken) = await LoginAsync(email);

        var me = await GetMeAsync(accessToken);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await me.Content.ReadFromJsonAsync<UserResponse>(CancellationToken)).ShouldBe(registered);

        var refresh = await PostWithCookieAsync("/api/auth/refresh", refreshToken);
        refresh.StatusCode.ShouldBe(HttpStatusCode.OK);
        var rotatedToken = ReadRefreshCookie(refresh);
        rotatedToken.ShouldNotBe(refreshToken);
        var refreshedAccess = await refresh.Content.ReadFromJsonAsync<AccessTokenResponse>(CancellationToken);
        (await GetMeAsync(refreshedAccess!.AccessToken)).StatusCode.ShouldBe(HttpStatusCode.OK);

        var logout = await PostWithCookieAsync("/api/auth/logout", rotatedToken);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await PostWithCookieAsync("/api/auth/refresh", rotatedToken)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_the_whole_session()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);
        var (_, original) = await LoginAsync(email);

        var firstRefresh = await PostWithCookieAsync("/api/auth/refresh", original);
        var rotated = ReadRefreshCookie(firstRefresh);

        // The old token is presented again, e.g. by an attacker who copied it.
        (await PostWithCookieAsync("/api/auth/refresh", original)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // The legitimate successor is revoked as well.
        (await PostWithCookieAsync("/api/auth/refresh", rotated)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Registering_an_existing_email_returns_conflict()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Registering_with_a_weak_password_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/register", new { email = UniqueEmail(), password = "alllowercase" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(CancellationToken);
        problem!.Errors.ShouldContainKey("Password");
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_unauthorized()
    {
        var email = UniqueEmail();
        await RegisterAsync(email);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1" }, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task Me_without_token_returns_unauthorized()
    {
        var response = await _client.GetAsync("/api/auth/me", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_unauthorized()
    {
        var response = await _client.PostAsync("/api/auth/refresh", content: null, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private async Task RegisterAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password = Password }, CancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<(string AccessToken, string RefreshToken)> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = Password }, CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(CancellationToken);
        return (body!.AccessToken, ReadRefreshCookie(response));
    }

    private Task<HttpResponseMessage> GetMeAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _client.SendAsync(request, CancellationToken);
    }

    // The cookie is Secure, which the test client's cookie container would not send over http,
    // so the tests pass it explicitly.
    private Task<HttpResponseMessage> PostWithCookieAsync(string path, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        return _client.SendAsync(request, CancellationToken);
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("refresh_token=", StringComparison.Ordinal));

        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("samesite=strict", Case.Insensitive);
        cookie.ShouldContain("path=/api/auth", Case.Insensitive);

        return cookie["refresh_token=".Length..cookie.IndexOf(';', StringComparison.Ordinal)];
    }
}
