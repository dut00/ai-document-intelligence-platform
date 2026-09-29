using System.Collections.Concurrent;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Domain.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIntelligence.Infrastructure.DomainEvents;

/// <summary>
/// Invokes every <see cref="IDomainEventHandler{TEvent}"/> registered for an event's runtime type.
/// </summary>
internal sealed class DomainEventDispatcher(IServiceProvider serviceProvider)
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> _invokers = new();

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var invoker = _invokers.GetOrAdd(
                domainEvent.GetType(),
                eventType => (HandlerInvoker)Activator.CreateInstance(typeof(HandlerInvoker<>).MakeGenericType(eventType))!);

            await invoker.InvokeAsync(serviceProvider, domainEvent, cancellationToken);
        }
    }

    // Bridges the runtime event type to the generic handler interface without reflection on every call.
    private abstract class HandlerInvoker
    {
        public abstract Task InvokeAsync(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken);
    }

    private sealed class HandlerInvoker<TEvent> : HandlerInvoker
        where TEvent : IDomainEvent
    {
        public override async Task InvokeAsync(IServiceProvider services, IDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            {
                await handler.HandleAsync((TEvent)domainEvent, cancellationToken);
            }
        }
    }
}
