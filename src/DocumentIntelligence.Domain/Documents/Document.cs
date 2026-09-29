using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents.Analysis;
using DocumentIntelligence.Domain.Documents.Events;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Domain.Documents;

/// <summary>
/// An uploaded document and its processing lifecycle:
/// Pending → Processing → Completed | Failed (Pending may also fail directly).
/// </summary>
public sealed class Document : AggregateRoot<DocumentId>
{
    public const int MaxFailureReasonLength = 1000;

    private bool _deletionRequested;

    private Document(
        DocumentId id,
        UserId ownerId,
        FileName fileName,
        ContentType contentType,
        FileSize size,
        DateTimeOffset uploadedAt)
        : base(id)
    {
        OwnerId = ownerId;
        FileName = fileName;
        ContentType = contentType;
        Size = size;
        StorageKey = StorageKey.For(ownerId, id);
        Status = DocumentStatus.Pending;
        UploadedAt = uploadedAt;
        UpdatedAt = uploadedAt;
    }

    public UserId OwnerId { get; }

    public FileName FileName { get; }

    public ContentType ContentType { get; }

    public FileSize Size { get; }

    public StorageKey StorageKey { get; }

    public DocumentStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset UploadedAt { get; }

    /// <summary>
    /// Time of the last status change; used to detect processing that got stuck.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ProcessedAt { get; private set; }

    public DocumentAnalysis? Analysis { get; private set; }

    public static Document Upload(
        DocumentId id,
        UserId ownerId,
        FileName fileName,
        ContentType contentType,
        FileSize size,
        DateTimeOffset now)
    {
        if (fileName.Extension != contentType.Extension)
        {
            throw new DomainException(
                $"File extension '{fileName.Extension}' does not match content type '{contentType}'.");
        }

        var document = new Document(id, ownerId, fileName, contentType, size, now);
        document.Raise(new DocumentUploadedDomainEvent(id, ownerId));

        return document;
    }

    /// <summary>
    /// A pending document can always be picked up. A document already in processing can be
    /// reclaimed only when it has not changed for <paramref name="staleAfter"/>, e.g. after a worker crash.
    /// </summary>
    public bool CanStartProcessing(DateTimeOffset now, TimeSpan staleAfter) =>
        Status == DocumentStatus.Pending
        || (Status == DocumentStatus.Processing && now - UpdatedAt >= staleAfter);

    public void StartProcessing(DateTimeOffset now, TimeSpan staleAfter)
    {
        if (!CanStartProcessing(now, staleAfter))
        {
            throw new DomainException($"Document {Id} cannot start processing from status {Status}.");
        }

        Status = DocumentStatus.Processing;
        UpdatedAt = now;
    }

    public void Complete(DocumentAnalysis analysis, DateTimeOffset now)
    {
        EnsureStatus(DocumentStatus.Processing, nameof(Complete));

        if (analysis.Id != Id)
        {
            throw new DomainException($"Analysis belongs to document {analysis.Id}, not {Id}.");
        }

        Analysis = analysis;
        Status = DocumentStatus.Completed;
        FailureReason = null;
        ProcessedAt = now;
        UpdatedAt = now;

        Raise(new DocumentProcessingCompletedDomainEvent(Id, OwnerId));
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        EnsureStatus(DocumentStatus.Pending, DocumentStatus.Processing, nameof(Fail));

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Failure reason must not be empty.");
        }

        reason = reason.Trim();
        FailureReason = reason.Length > MaxFailureReasonLength ? reason[..MaxFailureReasonLength] : reason;
        Status = DocumentStatus.Failed;
        ProcessedAt = now;
        UpdatedAt = now;

        Raise(new DocumentProcessingFailedDomainEvent(Id, OwnerId, FailureReason));
    }

    /// <summary>
    /// Allowed in any status: a worker that later receives this document's message acks and skips it.
    /// </summary>
    public void MarkForDeletion()
    {
        if (_deletionRequested)
        {
            throw new DomainException($"Document {Id} is already marked for deletion.");
        }

        _deletionRequested = true;
        Raise(new DocumentDeletedDomainEvent(Id, OwnerId, StorageKey));
    }

    private void EnsureStatus(DocumentStatus expected, string operation) =>
        EnsureStatus(expected, expected, operation);

    private void EnsureStatus(DocumentStatus first, DocumentStatus second, string operation)
    {
        if (Status != first && Status != second)
        {
            throw new DomainException($"Cannot {operation} document {Id} in status {Status}.");
        }
    }
}
