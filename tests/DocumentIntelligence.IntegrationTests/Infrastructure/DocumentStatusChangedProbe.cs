using DocumentIntelligence.Contracts.Documents;
using MassTransit;

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

/// <summary>
/// Subscribes to <see cref="DocumentStatusChanged"/> so tests can assert its delivery with the harness's
/// <c>Consumed</c> list: messages a consumer's outbox delivers do not show up in <c>Published</c>.
/// </summary>
public sealed class DocumentStatusChangedProbe : IConsumer<DocumentStatusChanged>
{
    public Task Consume(ConsumeContext<DocumentStatusChanged> context) => Task.CompletedTask;
}
