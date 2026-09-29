using System.ComponentModel.DataAnnotations;

namespace DocumentIntelligence.Api.RateLimiting;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Anonymous auth requests (register, login, refresh, logout) allowed per client IP address in each <see cref="AuthWindow"/>.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int AuthPermitLimit { get; init; } = 20;

    public TimeSpan AuthWindow { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Uploads a user can make in a burst; every upload costs an AI analysis.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int UploadTokenLimit { get; init; } = 10;

    /// <summary>
    /// Uploads regained per <see cref="UploadReplenishmentPeriod"/>, i.e. the sustained rate.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int UploadTokensPerPeriod { get; init; } = 2;

    public TimeSpan UploadReplenishmentPeriod { get; init; } = TimeSpan.FromMinutes(1);
}
