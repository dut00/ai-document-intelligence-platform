using DocumentIntelligence.Domain.Documents.Analysis;

namespace DocumentIntelligence.Application.Documents.Processing;

/// <summary>
/// Checks dates found by the AI against the weekend and the public-holiday calendar.
/// </summary>
public interface IDateInsightsService
{
    /// <summary>
    /// Returns the dates with a <see cref="CalendarCheck"/> attached, or unchanged when the
    /// holiday calendar is unavailable. Never fails because of the calendar.
    /// </summary>
    Task<IReadOnlyList<ImportantDate>> AddCalendarChecksAsync(
        IReadOnlyList<ImportantDate> dates,
        CancellationToken cancellationToken);
}
