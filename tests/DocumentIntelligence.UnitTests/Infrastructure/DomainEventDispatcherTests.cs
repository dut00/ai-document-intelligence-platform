using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Events;
using DocumentIntelligence.Domain.Users;
using DocumentIntelligence.Infrastructure.DomainEvents;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace DocumentIntelligence.UnitTests.Infrastructure;

public sealed class DomainEventDispatcherTests
{
    [Fact]
    public async Task Dispatch_invokes_every_handler_registered_for_the_event_runtime_type()
    {
        var first = Substitute.For<IDomainEventHandler<DocumentUploadedDomainEvent>>();
        var second = Substitute.For<IDomainEventHandler<DocumentUploadedDomainEvent>>();
        var unrelated = Substitute.For<IDomainEventHandler<DocumentDeletedDomainEvent>>();
        await using var services = new ServiceCollection()
            .AddSingleton(first)
            .AddSingleton(second)
            .AddSingleton(unrelated)
            .BuildServiceProvider();
        IDomainEvent domainEvent = new DocumentUploadedDomainEvent(DocumentId.New(), UserId.New());

        await new DomainEventDispatcher(services).DispatchAsync([domainEvent], TestContext.Current.CancellationToken);

        await first.Received(1).HandleAsync((DocumentUploadedDomainEvent)domainEvent, Arg.Any<CancellationToken>());
        await second.Received(1).HandleAsync((DocumentUploadedDomainEvent)domainEvent, Arg.Any<CancellationToken>());
        await unrelated.DidNotReceiveWithAnyArgs().HandleAsync(default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Dispatch_without_handlers_does_nothing()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();

        await new DomainEventDispatcher(services).DispatchAsync(
            [new DocumentUploadedDomainEvent(DocumentId.New(), UserId.New())],
            TestContext.Current.CancellationToken);
    }
}
