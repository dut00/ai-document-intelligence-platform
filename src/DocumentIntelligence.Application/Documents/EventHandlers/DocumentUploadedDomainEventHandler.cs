using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents.Events;

namespace DocumentIntelligence.Application.Documents.EventHandlers;

internal sealed class DocumentUploadedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<DocumentUploadedDomainEvent>
{
    public Task HandleAsync(DocumentUploadedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        publisher.PublishAsync(
            new DocumentUploaded(domainEvent.DocumentId.Value, domainEvent.OwnerId.Value),
            cancellationToken);
}
