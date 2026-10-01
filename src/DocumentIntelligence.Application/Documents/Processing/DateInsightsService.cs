using DocumentIntelligence.Application.Abstractions.Calendar;
using DocumentIntelligence.Domain.Documents.Analysis;
using Microsoft.Extensions.Logging;

namespace DocumentIntelligence.Application.Documents.Processing;

/// <summary>
/// Weekends are computed locally; public holidays come from <see cref="IPublicHolidayProvider"/>,
/// fetched at most once per year involved (including a following year a next business day may fall into).
/// </summary>
/// <remarks>
/// The dates come from the model, which reads untrusted text: a document listing dates in fifty different
/// years would otherwise mean fifty sequential calendar requests. Only the <see cref="MaxCheckedYears"/>
/// years closest to today are checked; dates in other years are saved without a check.
/// </remarks>
internal sealed partial class DateInsightsService(
    IPublicHolidayProvider holidayProvider,
    TimeProvider timeProvider,
    ILogger<DateInsightsService> logger)
    : IDateInsightsService
{
    public const int MaxCheckedYears = 5;

    public async Task<IReadOnlyList<ImportantDate>> AddCalendarChecksAsync(
        IReadOnlyList<ImportantDate> dates,
        CancellationToken cancellationToken)
    {
        if (dates.Count == 0)
        {
            return dates;
        }

        var holidaysByYear = new Dictionary<int, Dictionary<DateOnly, string>>();
        var checkedYears = SelectCheckedYears(dates);

        try
        {
            var checkedDates = new List<ImportantDate>(dates.Count);
            foreach (var date in dates)
            {
                if (!checkedYears.Contains(date.Date.Year))
                {
                    checkedDates.Add(date);
                    continue;
                }

                var calendarCheck = await CheckAsync(date.Date, holidaysByYear, cancellationToken);
                checkedDates.Add(date.WithCalendarCheck(calendarCheck));
            }

            return checkedDates;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The dates are still worth saving; the UI shows that the calendar check is unavailable.
            LogCalendarUnavailable(logger, exception);
            return dates;
        }
    }

    private HashSet<int> SelectCheckedYears(IReadOnlyList<ImportantDate> dates)
    {
        var currentYear = timeProvider.GetUtcNow().Year;
        var years = dates.Select(date => date.Date.Year).Distinct().ToList();

        var checkedYears = years
            .OrderBy(year => Math.Abs(year - currentYear))
            .ThenBy(year => year)
            .Take(MaxCheckedYears)
            .ToHashSet();

        if (checkedYears.Count < years.Count)
        {
            LogYearsNotChecked(logger, years.Count - checkedYears.Count, MaxCheckedYears);
        }

        return checkedYears;
    }

    private async Task<CalendarCheck> CheckAsync(
        DateOnly date,
        Dictionary<int, Dictionary<DateOnly, string>> holidaysByYear,
        CancellationToken cancellationToken)
    {
        var holidayName = await FindHolidayAsync(date, holidaysByYear, cancellationToken);

        var nextBusinessDay = date.AddDays(1);
        while (IsWeekend(nextBusinessDay) || await FindHolidayAsync(nextBusinessDay, holidaysByYear, cancellationToken) is not null)
        {
            nextBusinessDay = nextBusinessDay.AddDays(1);
        }

        return new CalendarCheck(IsWeekend(date), holidayName, nextBusinessDay);
    }

    private async Task<string?> FindHolidayAsync(
        DateOnly date,
        Dictionary<int, Dictionary<DateOnly, string>> holidaysByYear,
        CancellationToken cancellationToken)
    {
        if (!holidaysByYear.TryGetValue(date.Year, out var holidays))
        {
            var yearHolidays = await holidayProvider.GetPublicHolidaysAsync(date.Year, cancellationToken);

            // Several holidays may share a day; the first name is enough for a badge.
            holidays = yearHolidays
                .GroupBy(holiday => holiday.Date)
                .ToDictionary(group => group.Key, group => group.First().Name);
            holidaysByYear[date.Year] = holidays;
        }

        return holidays.GetValueOrDefault(date);
    }

    private static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    [LoggerMessage(Level = LogLevel.Information, Message = "Dates in {Count} more years were saved without a calendar check (at most {MaxYears} years are checked per document)")]
    private static partial void LogYearsNotChecked(ILogger logger, int count, int maxYears);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Public holiday calendar unavailable; saving dates without a calendar check")]
    private static partial void LogCalendarUnavailable(ILogger logger, Exception exception);
}
