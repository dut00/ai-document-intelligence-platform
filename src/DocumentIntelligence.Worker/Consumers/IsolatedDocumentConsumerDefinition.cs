using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// One message at a time, and only one fetched ahead: when the Worker dies, the only unacknowledged
/// message on this endpoint is the one being processed.
/// </summary>
public sealed class IsolatedDocumentConsumerDefinition : ConsumerDefinition<IsolatedDocumentConsumer>
{
    public const string QueueName = "document-processing-isolated";

    /// <summary>
    /// The endpoint's exchange, which forwards to its queue. A "queue:" address would make the sender
    /// declare the queue as a classic one, which RabbitMQ refuses for this quorum queue.
    /// </summary>
    public static readonly Uri QueueAddress = new($"exchange:{QueueName}");

    public IsolatedDocumentConsumerDefinition()
    {
        EndpointName = QueueName;
        ConcurrentMessageLimit = 1;
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<IsolatedDocumentConsumer> consumerConfigurator,
        IRegistrationContext context) =>
        endpointConfigurator.PrefetchCount = 1;
}
