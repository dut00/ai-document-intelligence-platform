using System.Net.Http.Json;
using DocumentIntelligence.Application.Abstractions.Calendar;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Infrastructure.Calendar;

/// <summary>
/// Public holidays from <c>GET /api/v3/PublicHolidays/{year}/{countryCode}</c>, cached per year and country.
/// Failures are not cached, so the next document tries again.
/// </summary>
internal sealed class NagerDateHolidayProvider(HttpClient httpClient, IMemoryCache cache, IOptions<HolidaysOptions> options)
    : IPublicHolidayProvider
{
    public async Task<IReadOnlyList<PublicHoliday>> GetPublicHolidaysAsync(int year, CancellationToken cancellationToken)
    {
        var countryCode = options.Value.CountryCode;
        var cacheKey = (nameof(NagerDateHolidayProvider), year, countryCode);

        if (cache.TryGetValue(cacheKey, out IReadOnlyList<PublicHoliday>? cached))
        {
            return cached!;
        }

        var response = await httpClient.GetFromJsonAsync<NagerPublicHoliday[]>(
            $"api/v3/PublicHolidays/{year}/{countryCode}",
            cancellationToken);

        // Regional holidays are skipped: they are not days off in the whole country.
        IReadOnlyList<PublicHoliday> holidays = (response ?? [])
            .Where(holiday => holiday.Global)
            .Select(holiday => new PublicHoliday(holiday.Date, holiday.Name))
            .ToList();

        cache.Set(cacheKey, holidays, options.Value.CacheDuration);

        return holidays;
    }
}
