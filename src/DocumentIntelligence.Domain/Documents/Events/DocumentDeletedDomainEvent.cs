using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Domain.Documents.Events;

/// <summary>
/// Raised before the document row is removed, so its stored content can be cleaned up.
/// </summary>
public sealed record DocumentDeletedDomainEvent(DocumentId DocumentId, UserId OwnerId, StorageKey StorageKey) : IDomainEvent;
