using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents.Analysis;

public sealed record Money
{
    public Money(decimal amount, string currency)
    {
        var code = currency?.Trim().ToUpperInvariant();

        if (code is null || code.Length != 3 || !code.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException($"Currency '{currency}' is not a three-letter ISO 4217 code.");
        }

        Amount = amount;
        Currency = code;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public override string ToString() => $"{Amount} {Currency}";
}
