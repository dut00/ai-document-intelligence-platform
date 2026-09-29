namespace DocumentIntelligence.Application.Abstractions.Messaging;

/// <summary>
/// Publishes integration messages (Contracts) through the transactional outbox: a message is
/// stored with the current unit of work and sent to the broker only after it commits.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class;
}
