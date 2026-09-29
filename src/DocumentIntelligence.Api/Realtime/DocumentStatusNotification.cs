using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.Api.Realtime;

/// <summary>
/// Pushed to the owner when processing of a document finishes; the client then refetches what it shows.
/// </summary>
public sealed record DocumentStatusNotification(Guid DocumentId, DocumentStatus Status, string? FailureReason);
