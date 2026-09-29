using DocumentIntelligence.Domain.Documents.Analysis;

namespace DocumentIntelligence.Application.Documents;

/// <summary>
/// <see cref="CalendarCheck"/> is null when the holiday calendar was unavailable during processing.
/// </summary>
public sealed record ImportantDateResponse(
    DateOnly Date,
    ImportantDateType Type,
    string Description,
    CalendarCheckResponse? CalendarCheck)
{
    public static ImportantDateResponse From(ImportantDate date) => new(
        date.Date,
        date.Type,
        date.Description,
        date.CalendarCheck is { } check
            ? new CalendarCheckResponse(check.IsWeekend, check.IsPublicHoliday, check.HolidayName, check.IsBusinessDay, check.NextBusinessDay)
            : null);
}
