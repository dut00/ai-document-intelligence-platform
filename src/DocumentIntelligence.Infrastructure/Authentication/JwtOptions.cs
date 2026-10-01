using System.ComponentModel.DataAnnotations;

namespace DocumentIntelligence.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// Keys that appear in this public repository (appsettings.Development.json, and an earlier
    /// .env.example). Anyone could sign tokens with them, so they are refused outside Development.
    /// </summary>
    public static readonly IReadOnlySet<string> PublishedSigningKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "local-development-signing-key-not-for-production-use",
        "local-docker-signing-key-not-for-production-use",
    };

    [Required]
    public string Issuer { get; init; } = string.Empty;

    [Required]
    public string Audience { get; init; } = string.Empty;

    /// <summary>
    /// HMAC-SHA256 key; at least 32 characters. Never commit a production key.
    /// </summary>
    [Required]
    [MinLength(32)]
    public string SigningKey { get; init; } = string.Empty;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    [Range(typeof(TimeSpan), "01:00:00", "90.00:00:00")]
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(7);

    /// <summary>
    /// How long a just-rotated refresh token is still accepted while its successor is unused, for a
    /// client that never received the new cookie (e.g. the page reloaded mid-refresh). Keep it short:
    /// within it, a copied token is not detected as reuse.
    /// </summary>
    [Range(typeof(TimeSpan), "00:00:00", "00:05:00")]
    public TimeSpan RefreshTokenReuseGracePeriod { get; init; } = TimeSpan.FromSeconds(10);
}
