using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// The consumers hosted by the Worker; also used by the integration tests, which run them in-process.
/// </summary>
public static class WorkerConsumers
{
    public static IBusRegistrationConfigurator AddWorkerConsumers(this IBusRegistrationConfigurator bus)
    {
        bus.AddConsumer<DocumentUploadedConsumer>();
        bus.AddConsumer<DocumentUploadedFaultConsumer>();
        bus.AddConsumer<DocumentDeletedConsumer>();

        return bus;
    }
}
