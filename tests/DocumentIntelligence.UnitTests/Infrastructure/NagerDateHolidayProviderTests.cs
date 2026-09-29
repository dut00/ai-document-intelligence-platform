using System.Net;
using DocumentIntelligence.Application.Abstractions.Calendar;
using DocumentIntelligence.Infrastructure.Calendar;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.UnitTests.Infrastructure;

public sealed class NagerDateHolidayProviderTests : IDisposable
{
    private const string Holidays2026 = """
        [
          { "date": "2026-01-01", "localName": "Nowy Rok", "name": "New Year's Day", "countryCode": "PL", "global": true, "counties": null, "types": ["Public"] },
          { "date": "2026-11-11", "localName": "Narodowe Święto Niepodległości", "name": "National Independence Day", "countryCode": "PL", "global": true, "counties": null, "types": ["Public"] },
          { "date": "2026-03-17", "localName": "Saint Patrick's Day", "name": "Saint Patrick's Day", "countryCode": "GB", "global": false, "counties": ["GB-NIR"], "types": ["Public"] }
        ]
        """;

    private readonly StubHttpMessageHandler _handler = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly NagerDateHolidayProvider _provider;

    public NagerDateHolidayProviderTests() =>
        _provider = new NagerDateHolidayProvider(
            new HttpClient(_handler) { BaseAddress = new Uri("https://date.nager.at/") },
            _cache,
            Options.Create(new HolidaysOptions { CountryCode = "PL" }));

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task Returns_nationwide_holidays_of_the_configured_country()
    {
        _handler.RespondWith(HttpStatusCode.OK, Holidays2026);

        var holidays = await _provider.GetPublicHolidaysAsync(2026, CancellationToken);

        holidays.ShouldBe(
        [
            new PublicHoliday(new DateOnly(2026, 1, 1), "New Year's Day"),
            new PublicHoliday(new DateOnly(2026, 11, 11), "National Independence Day"),
        ]);
        _handler.Requests.ShouldHaveSingleItem().Request.RequestUri.ShouldBe(
            new Uri("https://date.nager.at/api/v3/PublicHolidays/2026/PL"));
    }

    [Fact]
    public async Task Caches_holidays_per_year()
    {
        _handler.RespondWith(HttpStatusCode.OK, Holidays2026).RespondWith(HttpStatusCode.OK, "[]");

        await _provider.GetPublicHolidaysAsync(2026, CancellationToken);
        await _provider.GetPublicHolidaysAsync(2026, CancellationToken);
        await _provider.GetPublicHolidaysAsync(2027, CancellationToken);

        _handler.Requests.Select(request => request.Request.RequestUri!.AbsolutePath).ShouldBe(
            ["/api/v3/PublicHolidays/2026/PL", "/api/v3/PublicHolidays/2027/PL"]);
    }

    [Fact]
    public async Task Failure_is_reported_and_not_cached()
    {
        _handler.RespondWith(HttpStatusCode.ServiceUnavailable, "{}").RespondWith(HttpStatusCode.OK, Holidays2026);

        await Should.ThrowAsync<HttpRequestException>(() => _provider.GetPublicHolidaysAsync(2026, CancellationToken));
        var holidays = await _provider.GetPublicHolidaysAsync(2026, CancellationToken);

        holidays.Count.ShouldBe(2);
    }
}
