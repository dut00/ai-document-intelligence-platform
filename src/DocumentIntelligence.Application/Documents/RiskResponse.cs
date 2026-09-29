using DocumentIntelligence.Domain.Documents.Analysis;

namespace DocumentIntelligence.Application.Documents;

public sealed record RiskResponse(RiskSeverity Severity, string Description);
