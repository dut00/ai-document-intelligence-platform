using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents.Analysis;

public enum ImportantDateType
{
    StartDate,
    EndDate,
    EffectiveDate,
    SigningDate,
    PaymentDeadline,
    TerminationDeadline,
    ExpiryDate,
    Other,
}

/// <summary>
/// A date identified in a document and what it means. The calendar check is attached
/// afterwards and stays null when the holiday calendar was unavailable.
/// </summary>
public sealed record ImportantDate
{
    public ImportantDate(DateOnly date, ImportantDateType type, string description, CalendarCheck? calendarCheck = null)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Important date description must not be empty.");
        }

        if (calendarCheck is not null && calendarCheck.NextBusinessDay <= date)
        {
            throw new DomainException("Next business day must be after the checked date.");
        }

        Date = date;
        Type = type;
        Description = description.Trim();
        CalendarCheck = calendarCheck;
    }

    public DateOnly Date { get; }

    public ImportantDateType Type { get; }

    public string Description { get; }

    public CalendarCheck? CalendarCheck { get; }

    public ImportantDate WithCalendarCheck(CalendarCheck calendarCheck) =>
        new(Date, Type, Description, calendarCheck);
}
