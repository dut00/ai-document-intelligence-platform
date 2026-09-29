using System.Linq.Expressions;
using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.Application.Documents;

public sealed record DocumentSummary(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentStatus Status,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ProcessedAt)
{
    /// <summary>
    /// Used in queries, where EF Core reads only the columns it needs.
    /// </summary>
    public static readonly Expression<Func<Document, DocumentSummary>> Projection = document => new DocumentSummary(
        document.Id.Value,
        document.FileName.Value,
        document.ContentType.MimeType,
        document.Size.Bytes,
        document.Status,
        document.UploadedAt,
        document.ProcessedAt);

    private static readonly Func<Document, DocumentSummary> _fromDocument = Projection.Compile();

    public static DocumentSummary From(Document document) => _fromDocument(document);
}
