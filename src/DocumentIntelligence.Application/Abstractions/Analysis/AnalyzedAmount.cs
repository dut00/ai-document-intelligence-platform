namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <param name="Currency">A three-letter ISO 4217 code, e.g. "PLN".</param>
public sealed record AnalyzedAmount(string Description, decimal Amount, string Currency);
