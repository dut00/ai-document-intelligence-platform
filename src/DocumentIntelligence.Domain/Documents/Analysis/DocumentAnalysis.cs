using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents.Analysis;

/// <summary>
/// The AI analysis of a document. Owned by the <see cref="Document"/> aggregate (1:1, same id).
/// </summary>
public sealed class DocumentAnalysis : Entity<DocumentId>
{
    // Parameter names match the properties so EF Core can bind this constructor.
    private DocumentAnalysis(
        DocumentId id,
        string documentType,
        string summary,
        IReadOnlyList<ExtractedEntity> entities,
        IReadOnlyList<ImportantDate> importantDates,
        IReadOnlyList<FinancialItem> financialInformation,
        IReadOnlyList<Risk> potentialRisks,
        string model,
        DateTimeOffset createdAt)
        : base(id)
    {
        DocumentType = documentType;
        Summary = summary;
        Entities = entities;
        ImportantDates = importantDates;
        FinancialInformation = financialInformation;
        PotentialRisks = potentialRisks;
        Model = model;
        CreatedAt = createdAt;
    }

    /// <summary>
    /// Free-form classification, e.g. "Invoice" or "Employment contract".
    /// </summary>
    public string DocumentType { get; }

    public string Summary { get; }

    public IReadOnlyList<ExtractedEntity> Entities { get; }

    public IReadOnlyList<ImportantDate> ImportantDates { get; }

    public IReadOnlyList<FinancialItem> FinancialInformation { get; }

    public IReadOnlyList<Risk> PotentialRisks { get; }

    /// <summary>
    /// The model that produced the analysis, e.g. "claude-haiku-4-5-20251001" or "fake".
    /// </summary>
    public string Model { get; }

    public DateTimeOffset CreatedAt { get; }

    public static DocumentAnalysis Create(
        DocumentId documentId,
        string documentType,
        string summary,
        IEnumerable<ExtractedEntity> entities,
        IEnumerable<ImportantDate> importantDates,
        IEnumerable<FinancialItem> financialInformation,
        IEnumerable<Risk> potentialRisks,
        string model,
        DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(documentType))
        {
            throw new DomainException("Document type must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            throw new DomainException("Summary must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new DomainException("Model must not be empty.");
        }

        return new DocumentAnalysis(
            documentId,
            documentType.Trim(),
            summary.Trim(),
            [.. entities],
            [.. importantDates],
            [.. financialInformation],
            [.. potentialRisks],
            model,
            createdAt);
    }
}
