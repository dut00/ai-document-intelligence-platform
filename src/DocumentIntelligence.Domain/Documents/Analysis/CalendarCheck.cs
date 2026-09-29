namespace DocumentIntelligence.Domain.Documents.Analysis;

/// <summary>
/// Result of checking a date against the weekend and the public-holiday calendar.
/// Computed deterministically by the backend, never by the AI.
/// </summary>
public sealed record CalendarCheck
{
    public CalendarCheck(bool isWeekend, string? holidayName, DateOnly nextBusinessDay)
    {
        IsWeekend = isWeekend;
        HolidayName = string.IsNullOrWhiteSpace(holidayName) ? null : holidayName.Trim();
        NextBusinessDay = nextBusinessDay;
    }

    public bool IsWeekend { get; }

    public string? HolidayName { get; }

    public bool IsPublicHoliday => HolidayName is not null;

    public bool IsBusinessDay => !IsWeekend && !IsPublicHoliday;

    /// <summary>
    /// The first business day strictly after the checked date.
    /// </summary>
    public DateOnly NextBusinessDay { get; }
}
