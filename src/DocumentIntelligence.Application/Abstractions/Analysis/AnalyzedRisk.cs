namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <param name="Severity">A name of <c>RiskSeverity</c>, e.g. "High".</param>
public sealed record AnalyzedRisk(string Severity, string Description);
