using DocumentIntelligence.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DocumentIntelligence.Infrastructure.DomainEvents;

/// <summary>
/// Dispatches the domain events of tracked aggregates just before <c>SaveChanges</c> writes them.
/// Handlers run inside the same unit of work, so the outbox messages they publish are committed
/// in the same transaction as the aggregate changes, or not at all.
/// </summary>
internal sealed class DispatchDomainEventsInterceptor(DomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await dispatcher.DispatchAsync(CollectDomainEvents(context), cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        // Handlers are asynchronous; blocking on them here could deadlock.
        if (eventData.Context?.ChangeTracker.Entries<IAggregateRoot>().Any(entry => entry.Entity.DomainEvents.Count > 0) == true)
        {
            throw new InvalidOperationException("Aggregates with domain events must be saved with SaveChangesAsync.");
        }

        return base.SavingChanges(eventData, result);
    }

    private static List<IDomainEvent> CollectDomainEvents(DbContext context)
    {
        var aggregates = context.ChangeTracker.Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        var domainEvents = aggregates.SelectMany(aggregate => aggregate.DomainEvents).ToList();

        // Cleared up front so a retried save does not publish the same events twice.
        aggregates.ForEach(aggregate => aggregate.ClearDomainEvents());

        return domainEvents;
    }
}
