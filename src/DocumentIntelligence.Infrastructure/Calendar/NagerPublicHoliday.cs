namespace DocumentIntelligence.Infrastructure.Calendar;

/// <summary>
/// An entry of the Nager.Date <c>PublicHolidays</c> response (only the fields used here).
/// </summary>
/// <param name="Global">False for holidays observed only in some regions of the country.</param>
internal sealed record NagerPublicHoliday(DateOnly Date, string LocalName, string Name, bool Global);
