using DocumentIntelligence.Api.Extensions;
using DocumentIntelligence.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace DocumentIntelligence.Api.Realtime;

/// <summary>
/// Live status updates of the caller's documents. Clients only listen: the hub has no callable methods.
/// </summary>
/// <remarks>
/// Browsers cannot send headers with a WebSocket, so the access token may also come in the
/// <c>access_token</c> query parameter (see <see cref="RealtimeExtensions"/>).
/// </remarks>
[Authorize]
public sealed class DocumentsHub : Hub<IDocumentsClient>
{
    public const string Path = "/hubs/documents";

    public static string GroupFor(UserId userId) => $"user:{userId.Value}";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.GetUserId() is not { } userId)
        {
            Context.Abort();
            return;
        }

        // One group per user: every tab and device of the owner gets the update.
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId));
        await base.OnConnectedAsync();
    }
}
