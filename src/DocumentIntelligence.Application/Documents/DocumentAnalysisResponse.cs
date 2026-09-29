using DocumentIntelligence.Domain.Documents.Analysis;

namespace DocumentIntelligence.Application.Documents;

public sealed record DocumentAnalysisResponse(
    string DocumentType,
    string Summary,
    IReadOnlyList<EntityResponse> Entities,
    IReadOnlyList<ImportantDateResponse> ImportantDates,
    IReadOnlyList<FinancialItemResponse> FinancialInformation,
    IReadOnlyList<RiskResponse> PotentialRisks,
    string Model,
    DateTimeOffset CreatedAt)
{
    public static DocumentAnalysisResponse From(DocumentAnalysis analysis) => new(
        analysis.DocumentType,
        analysis.Summary,
        [.. analysis.Entities.Select(entity => new EntityResponse(entity.Type, entity.Name))],
        [.. analysis.ImportantDates.Select(ImportantDateResponse.From)],
        [.. analysis.FinancialInformation.Select(
            item => new FinancialItemResponse(item.Description, item.Amount.Amount, item.Amount.Currency))],
        [.. analysis.PotentialRisks.Select(risk => new RiskResponse(risk.Severity, risk.Description))],
        analysis.Model,
        analysis.CreatedAt);
}
