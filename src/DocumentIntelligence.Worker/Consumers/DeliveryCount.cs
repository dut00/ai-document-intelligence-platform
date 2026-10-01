using System.Globalization;
using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// How many earlier deliveries of a message went unacknowledged, e.g. because the Worker holding it died.
/// RabbitMQ quorum queues report it in transport headers (<c>context.Headers</c> holds the message
/// envelope's, not these); absent on a first delivery and on other transports.
/// </summary>
public static class DeliveryCount
{
    public const string DeliveryCountHeader = "x-delivery-count";

    // RabbitMQ 4 also counts every return to the queue under its own name.
    public const string AcquiredCountHeader = "x-acquired-count";

    public static int Of(ConsumeContext context) =>
        Math.Max(Read(context, DeliveryCountHeader), Read(context, AcquiredCountHeader));

    private static int Read(ConsumeContext context, string header) =>
        context.ReceiveContext.TransportHeaders.TryGetHeader(header, out var value) && value is not null
            ? Convert.ToInt32(value, CultureInfo.InvariantCulture)
            : 0;
}
