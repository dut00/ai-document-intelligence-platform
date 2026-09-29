using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents.Analysis;

/// <summary>
/// An amount found in a document together with what it refers to, e.g. "Total due".
/// </summary>
public sealed record FinancialItem
{
    public FinancialItem(string description, Money amount)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Financial item description must not be empty.");
        }

        Description = description.Trim();
        Amount = amount ?? throw new DomainException("Financial item amount is required.");
    }

    public string Description { get; }

    public Money Amount { get; }
}
