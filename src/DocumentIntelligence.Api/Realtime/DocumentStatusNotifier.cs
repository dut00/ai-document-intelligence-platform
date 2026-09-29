using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using MassTransit;
using Microsoft.AspNetCore.SignalR;

namespace DocumentIntelligence.Api.Realtime;

/// <summary>
/// Forwards the Worker's status changes to the owner's SignalR connections on this API instance.
/// </summary>
public sealed class DocumentStatusNotifier(IHubContext<DocumentsHub, IDocumentsClient> hub) : IConsumer<DocumentStatusChanged>
{
    public Task Consume(ConsumeContext<DocumentStatusChanged> context)
    {
        var message = context.Message;
        var notification = new DocumentStatusNotification(
            message.DocumentId,
            Enum.Parse<DocumentStatus>(message.Status),
            message.FailureReason);

        return hub.Clients.Group(DocumentsHub.GroupFor(new UserId(message.OwnerId))).DocumentStatusChanged(notification);
    }
}
