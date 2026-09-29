namespace DocumentIntelligence.Api.Authentication;

/// <summary>
/// The refresh token travels only in an httpOnly cookie scoped to the auth endpoints,
/// so page scripts can never read it. The access token stays in the client's memory.
/// </summary>
internal static class RefreshTokenCookie
{
    public const string Name = "refresh_token";
    public const string Path = "/api/auth";

    public static string? Read(HttpRequest request) => request.Cookies[Name];

    public static void Write(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, CreateOptions(expiresAt));

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, CreateOptions(expiresAt: null));

    // Browsers treat http://localhost as a secure context, so Secure works in local development too.
    private static CookieOptions CreateOptions(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
