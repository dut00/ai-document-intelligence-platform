using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// Bounds the AI calls (and PDF parsing) a Worker runs at once, however many uploads arrive, and fetches
/// no more ahead than it processes: every unacknowledged message is returned when the Worker dies.
/// </summary>
public sealed class DocumentUploadedConsumerDefinition : ConsumerDefinition<DocumentUploadedConsumer>
{
    public const int MaxConcurrentDocuments = 4;

    public DocumentUploadedConsumerDefinition() => ConcurrentMessageLimit = MaxConcurrentDocuments;

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<DocumentUploadedConsumer> consumerConfigurator,
        IRegistrationContext context) =>
        endpointConfigurator.PrefetchCount = MaxConcurrentDocuments;
}
