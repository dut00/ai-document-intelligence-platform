using DocumentIntelligence.Application.Abstractions.Messaging;
using MassTransit;

namespace DocumentIntelligence.Infrastructure.Messaging;

/// <summary>
/// The scoped <see cref="IPublishEndpoint"/> writes to the Entity Framework outbox,
/// so messages are sent only after the surrounding <c>SaveChanges</c> commits.
/// </summary>
internal sealed class MassTransitIntegrationEventPublisher(IPublishEndpoint publishEndpoint) : IIntegrationEventPublisher
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class =>
        publishEndpoint.Publish(message, cancellationToken);
}
