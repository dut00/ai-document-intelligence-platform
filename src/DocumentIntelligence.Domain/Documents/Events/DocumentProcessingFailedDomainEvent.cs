using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Domain.Documents.Events;

public sealed record DocumentProcessingFailedDomainEvent(DocumentId DocumentId, UserId OwnerId, string Reason) : IDomainEvent;
