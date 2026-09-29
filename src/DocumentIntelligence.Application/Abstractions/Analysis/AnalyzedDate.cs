namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <param name="Date">The date in <see cref="AnalysisResult.DateFormat"/>.</param>
/// <param name="Type">A name of <c>ImportantDateType</c>, e.g. "PaymentDeadline".</param>
public sealed record AnalyzedDate(string Date, string Type, string Description);
