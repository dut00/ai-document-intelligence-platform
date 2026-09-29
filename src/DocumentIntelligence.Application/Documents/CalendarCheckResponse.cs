namespace DocumentIntelligence.Application.Documents;

public sealed record CalendarCheckResponse(
    bool IsWeekend,
    bool IsPublicHoliday,
    string? HolidayName,
    bool IsBusinessDay,
    DateOnly NextBusinessDay);
