namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <summary>
/// The analysis as returned by the AI. Values stay raw (enum names and dates as strings) so an
/// invalid response can be reported back to the model instead of failing deserialization.
/// </summary>
public sealed record AnalysisResult(
    string DocumentType,
    string Summary,
    IReadOnlyList<AnalyzedEntity> Entities,
    IReadOnlyList<AnalyzedDate> ImportantDates,
    IReadOnlyList<AnalyzedAmount> FinancialInformation,
    IReadOnlyList<AnalyzedRisk> PotentialRisks)
{
    public const string DateFormat = "yyyy-MM-dd";
}
