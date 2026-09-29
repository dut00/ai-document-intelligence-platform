namespace DocumentIntelligence.Domain.Abstractions;

/// <summary>
/// Non-generic view of an aggregate root, so infrastructure can collect domain events
/// from any tracked aggregate regardless of its id type.
/// </summary>
public interface IAggregateRoot
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
