using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Domain.Documents.Events;

public sealed record DocumentUploadedDomainEvent(DocumentId DocumentId, UserId OwnerId) : IDomainEvent;
