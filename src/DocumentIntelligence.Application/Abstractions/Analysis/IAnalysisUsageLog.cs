using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <summary>
/// An append-only record of AI analyses, one per document, for the daily cost limits. Entries are not
/// deleted with their documents, so deleting analyzed documents does not reset the counts.
/// </summary>
public interface IAnalysisUsageLog
{
    Task<bool> IsRecordedAsync(DocumentId documentId, CancellationToken cancellationToken);

    /// <summary>
    /// Records the analysis of a document and commits it at once, outside the caller's transaction: the
    /// AI is paid for even if processing later rolls back (e.g. the document was deleted meanwhile).
    /// Recording a document again has no effect.
    /// </summary>
    Task RecordAsync(UserId ownerId, DocumentId documentId, DateTimeOffset analyzedAt, CancellationToken cancellationToken);

    Task<int> CountSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);

    Task<int> CountForOwnerSinceAsync(UserId ownerId, DateTimeOffset since, CancellationToken cancellationToken);
}
