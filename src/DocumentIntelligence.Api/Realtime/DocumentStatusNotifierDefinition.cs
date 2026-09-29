using DocumentIntelligence.Infrastructure.Messaging;
using MassTransit;

namespace DocumentIntelligence.Api.Realtime;

/// <summary>
/// Each API instance holds its own SignalR connections, so each one needs every status change: the
/// notifier gets a queue per instance (unlike the Worker's competing consumers), deleted when the instance stops.
/// As a notification endpoint it skips the inbox/outbox and retries (see <see cref="NotificationEndpoints"/>).
/// </summary>
public sealed class DocumentStatusNotifierDefinition : ConsumerDefinition<DocumentStatusNotifier>
{
    public DocumentStatusNotifierDefinition() =>
        Endpoint(endpoint =>
        {
            endpoint.Name = NotificationEndpoints.InstanceName("document-status");
            endpoint.Temporary = true;
        });
}
