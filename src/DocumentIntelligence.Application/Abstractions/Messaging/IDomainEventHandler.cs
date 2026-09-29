using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Application.Abstractions.Messaging;

/// <summary>
/// Reacts to a domain event inside the unit of work that raised it, before the transaction commits.
/// </summary>
public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
