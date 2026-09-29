using DocumentIntelligence.Application.Abstractions.Calendar;

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces the Nager.Date client so the tests do not depend on the internet.
/// </summary>
public sealed class StubPublicHolidayProvider : IPublicHolidayProvider
{
    public const string IndependenceDay = "Independence Day";

    public Task<IReadOnlyList<PublicHoliday>> GetPublicHolidaysAsync(int year, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PublicHoliday>>(
        [
            new PublicHoliday(new DateOnly(year, 1, 1), "New Year's Day"),
            new PublicHoliday(new DateOnly(year, 11, 11), IndependenceDay),
        ]);
}
