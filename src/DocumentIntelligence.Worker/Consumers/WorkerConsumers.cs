using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// The consumers hosted by the Worker; also used by the integration tests, which run them in-process.
/// </summary>
public static class WorkerConsumers
{
    public const string RoleSetting = "Worker:Role";

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
