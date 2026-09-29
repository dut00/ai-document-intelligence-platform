namespace DocumentIntelligence.Application.Abstractions.Calendar;

/// <summary>
/// Public holidays of the configured country.
/// </summary>
public interface IPublicHolidayProvider
{
    Task<IReadOnlyList<PublicHoliday>> GetPublicHolidaysAsync(int year, CancellationToken cancellationToken);
}
