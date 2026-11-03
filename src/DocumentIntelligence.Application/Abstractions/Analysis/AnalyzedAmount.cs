using System.Text.Json.Serialization;

namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <param name="Amount">Required when read from JSON: a missing value would otherwise become a real-looking 0.</param>
/// <param name="Currency">A three-letter ISO 4217 code, e.g. "PLN".</param>
public sealed record AnalyzedAmount(string Description, [property: JsonRequired] decimal Amount, string Currency);
