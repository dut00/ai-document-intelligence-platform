using System.Net;
using DocumentIntelligence.Api.RateLimiting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace DocumentIntelligence.IntegrationTests;

/// <summary>
/// The throttle on its own (no API host): the counters behind the login limits.
/// </summary>
public sealed class LoginAttemptThrottleTests : IDisposable
{
    private const string Email = "owner@example.com";

    private static readonly IPAddress _first = IPAddress.Parse("203.0.113.1");
    private static readonly IPAddress _second = IPAddress.Parse("203.0.113.2");
    private static readonly IPAddress _third = IPAddress.Parse("203.0.113.3");

    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly RateLimitingOptions _options = new()
    {
        LoginAttemptsPerAccount = 2,
        LoginAttemptsPerAccountOverall = 3,
        LoginOverBudgetDelay = TimeSpan.FromSeconds(3),
    };

    private readonly LoginAttemptThrottle _throttle;

    public LoginAttemptThrottleTests() =>
        _throttle = new LoginAttemptThrottle(_cache, new UpperInvariantLookupNormalizer(), Options.Create(_options), new FakeTimeProvider());

    public void Dispose() => _cache.Dispose();

    [Fact]
    public void Attempts_refused_for_one_address_do_not_use_up_the_account_budget()
    {
        Begin(_first).ShouldBe((true, TimeSpan.Zero));
        Begin(_first).ShouldBe((true, TimeSpan.Zero));
        for (var refused = 0; refused < 10; refused++)
        {
            Begin(_first).Allowed.ShouldBeFalse();
        }

        // Two attempts counted so far; the third is within the account budget, the fourth over it.
        Begin(_second).ShouldBe((true, TimeSpan.Zero));
        Begin(_third).ShouldBe((true, _options.LoginOverBudgetDelay));
    }

    [Fact]
    public void Success_clears_the_account_budget()
    {
        Begin(_first);
        Begin(_second);
        Begin(_third);
        Begin(_third).Delay.ShouldBe(_options.LoginOverBudgetDelay);

        _throttle.Succeeded(_third, Email);

        Begin(_second).ShouldBe((true, TimeSpan.Zero));
    }

    [Fact]
    public void Over_budget_attempts_are_delayed_not_refused()
    {
        for (var address = 1; address <= 10; address++)
        {
            var (allowed, delay) = Begin(IPAddress.Parse($"198.51.100.{address}"));

            allowed.ShouldBeTrue();
            delay.ShouldBe(address > _options.LoginAttemptsPerAccountOverall ? _options.LoginOverBudgetDelay : TimeSpan.Zero);
        }
    }

    private (bool Allowed, TimeSpan Delay) Begin(IPAddress client)
    {
        var allowed = _throttle.TryBeginAttempt(client, Email, out _, out var delay);
        return (allowed, delay);
    }
}
