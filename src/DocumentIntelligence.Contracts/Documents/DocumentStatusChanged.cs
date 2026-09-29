namespace DocumentIntelligence.Contracts.Documents;

/// <summary>
/// Processing of a document finished. <paramref name="Status"/> is "Completed" or "Failed".
/// </summary>
public sealed record DocumentStatusChanged(Guid DocumentId, Guid OwnerId, string Status, string? FailureReason);
