using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents.Events;

namespace DocumentIntelligence.Application.Documents.EventHandlers;

internal sealed class DocumentDeletedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<DocumentDeletedDomainEvent>
{
    public Task HandleAsync(DocumentDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        publisher.PublishAsync(
            new DocumentDeleted(domainEvent.DocumentId.Value, domainEvent.OwnerId.Value, domainEvent.StorageKey.Value),
            cancellationToken);
}
