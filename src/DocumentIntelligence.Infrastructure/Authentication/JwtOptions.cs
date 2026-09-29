using System.ComponentModel.DataAnnotations;

namespace DocumentIntelligence.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

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
}
