using DocumentIntelligence.Application.Documents.Processing;
using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// The consumers hosted by the Worker; also used by the integration tests, which run them in-process.
/// </summary>
public static class WorkerConsumers
{
    public const string RoleSetting = "Worker:Role";

    /// <summary>
    /// How long a stopping Worker lets the documents in hand finish before cancelling them: longer than
    /// one attempt may take (<see cref="ProcessDocumentCommand.ProcessingDeadline"/>).
    /// </summary>
    public static readonly TimeSpan ConsumerStopTimeout = ProcessDocumentCommand.ProcessingDeadline + TimeSpan.FromSeconds(30);

    /// <summary>
    /// The host's own shutdown limit, above <see cref="ConsumerStopTimeout"/>; the containers' stop grace
    /// period (docker-compose.yml) is longer still.
    /// </summary>
    public static readonly TimeSpan ShutdownTimeout = ConsumerStopTimeout + TimeSpan.FromMinutes(1);

    public static IBusRegistrationConfigurator AddWorkerConsumers(this IBusRegistrationConfigurator bus, WorkerRole role = WorkerRole.All)
    {
        if (role is WorkerRole.All or WorkerRole.Main)
        {
            bus.AddConsumer<DocumentUploadedConsumer, DocumentUploadedConsumerDefinition>();
            bus.AddConsumer<DocumentUploadedFaultConsumer>();
            bus.AddConsumer<DocumentDeletedConsumer>();
        }

        if (role is WorkerRole.All or WorkerRole.Isolated)
        {
            bus.AddConsumer<IsolatedDocumentConsumer, IsolatedDocumentConsumerDefinition>();
        }

        return bus;
    }
}
