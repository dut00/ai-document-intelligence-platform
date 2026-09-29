namespace DocumentIntelligence.Contracts.Documents;

/// <summary>
/// A document was deleted from the database; its stored content at <paramref name="StorageKey"/> can be removed.
/// </summary>
public sealed record DocumentDeleted(Guid DocumentId, Guid OwnerId, string StorageKey);
