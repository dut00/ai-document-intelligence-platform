using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Events;

namespace DocumentIntelligence.Application.Documents.EventHandlers;

/// <summary>
/// Announces the end of processing, e.g. so the API can notify the owner in real time.
/// </summary>
internal sealed class DocumentStatusChangedDomainEventHandler(IIntegrationEventPublisher publisher)
    : IDomainEventHandler<DocumentProcessingCompletedDomainEvent>,
      IDomainEventHandler<DocumentProcessingFailedDomainEvent>
{
    public Task HandleAsync(DocumentProcessingCompletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        publisher.PublishAsync(
            new DocumentStatusChanged(
                domainEvent.DocumentId.Value,
                domainEvent.OwnerId.Value,
                nameof(DocumentStatus.Completed),
                FailureReason: null),
            cancellationToken);

    public Task HandleAsync(DocumentProcessingFailedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        publisher.PublishAsync(
            new DocumentStatusChanged(
                domainEvent.DocumentId.Value,
                domainEvent.OwnerId.Value,
                nameof(DocumentStatus.Failed),
                domainEvent.Reason),
            cancellationToken);
}
