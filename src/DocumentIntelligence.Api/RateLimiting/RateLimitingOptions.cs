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
    /// Accounts one client IP address can create per <see cref="RegisterWindow"/>, on top of the auth limit.
    /// Every account gets its own daily AI allowance, so cheap accounts would multiply it: with the
    /// defaults one address gets at most 10 x 20 analyses a day, well below the overall daily limit.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int RegisterPermitLimit { get; init; } = 10;

    public TimeSpan RegisterWindow { get; init; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Login attempts without a success for one account from one client IP address within
    /// <see cref="LoginAttemptWindow"/>; further attempts from that address are refused. Unlike an account
    /// lockout, this does not let a stranger lock the owner out: the owner signs in from another address.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int LoginAttemptsPerAccount { get; init; } = 5;

    public TimeSpan LoginAttemptWindow { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Login attempts without a success for one account from all addresses together within
    /// <see cref="LoginAttemptWindow"/>. Beyond it, every attempt for the account waits
    /// <see cref="LoginOverBudgetDelay"/> before the password is checked: guessing from many addresses is
    /// slowed down, while the owner, unlike with a lockout, can still sign in.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int LoginAttemptsPerAccountOverall { get; init; } = 50;

    [Range(typeof(TimeSpan), "00:00:00", "00:00:30")]
    public TimeSpan LoginOverBudgetDelay { get; init; } = TimeSpan.FromSeconds(3);

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
