using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Api.RateLimiting;

/// <summary>
/// Limits login attempts per (client IP address, account): once <see cref="RateLimitingOptions.LoginAttemptsPerAccount"/>
/// attempts without a success were made within the window, that address is refused for the account.
/// It replaces an account lockout, which would let anyone lock a known email out by failing on purpose;
/// here the owner, coming from another address, is unaffected. Guessing across many accounts is bounded
/// by the per-IP auth limit.
/// </summary>
/// <remarks>
/// An attempt is counted before the password is checked, so concurrent requests cannot all slip in before
/// the first failure is recorded; a successful login clears the count. The counts live in memory, so each
/// API instance keeps its own: with N instances an attacker gets up to N times the attempts, still bounded
/// by the per-IP limit of each.
/// </remarks>
internal sealed class LoginAttemptThrottle(
    IMemoryCache cache,
    ILookupNormalizer normalizer,
    IOptions<RateLimitingOptions> options,
    TimeProvider timeProvider)
{
    // The login validator's limit; longer values never reach a password check.
    private const int MaxEmailLength = 256;

    // Static: the throttle is scoped (as Identity's normalizer is), its counters live in the shared cache.
    private static readonly Lock _gate = new();

    /// <summary>
    /// Counts an attempt, or refuses it when the budget for this address and account is used up.
    /// </summary>
    public bool TryBeginAttempt(IPAddress? client, string? email, out TimeSpan retryAfter)
    {
        retryAfter = TimeSpan.Zero;

        // Without a plausible email the request fails validation and checks no password: nothing to
        // throttle, and nothing of an arbitrarily long string is kept in memory.
        if (string.IsNullOrWhiteSpace(email) || email.Length > MaxEmailLength)
        {
            return true;
        }

        var attempts = GetOrCreate(Key(client, email));
        if (attempts.Increment() <= options.Value.LoginAttemptsPerAccount)
        {
            return true;
        }

        retryAfter = attempts.WindowEnd - timeProvider.GetUtcNow();
        return retryAfter <= TimeSpan.Zero;
    }

    public void Succeeded(IPAddress? client, string email)
    {
        if (email.Length <= MaxEmailLength)
        {
            cache.Remove(Key(client, email));
        }
    }

    private Attempts GetOrCreate(string key)
    {
        // Created under a lock: two first attempts racing must share one counter.
        lock (_gate)
        {
            return cache.GetOrCreate(key, entry =>
            {
                // The window starts with the first attempt and is not extended by later ones.
                var windowEnd = timeProvider.GetUtcNow() + options.Value.LoginAttemptWindow;
                entry.AbsoluteExpiration = windowEnd;
                return new Attempts(windowEnd);
            })!;
        }
    }

    // Normalized the way Identity looks the account up, so Unicode look-alikes of one email (e.g. the
    // Kelvin sign for "K") share its budget; hashed, so every key has the same small size.
    private string Key(IPAddress? client, string email)
    {
        var normalized = normalizer.NormalizeEmail(email.Trim()) ?? string.Empty;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));

        return string.Create(CultureInfo.InvariantCulture, $"login-attempts:{ClientPartition.For(client)}:{hash}");
    }

    private sealed class Attempts(DateTimeOffset windowEnd)
    {
        private int _count;

        public DateTimeOffset WindowEnd { get; } = windowEnd;

        public int Increment() => Interlocked.Increment(ref _count);
    }
}
