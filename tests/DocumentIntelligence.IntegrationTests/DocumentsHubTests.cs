using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using DocumentIntelligence.Api.Realtime;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace DocumentIntelligence.IntegrationTests;

/// <summary>
/// Worker → DocumentStatusChanged → the API's notifier → the SignalR group of the document's owner.
/// </summary>
public sealed class DocumentsHubTests(ApiFactory factory)
{
    private static readonly TimeSpan _notificationTimeout = TimeSpan.FromSeconds(15);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Owner_is_notified_when_processing_of_their_document_finishes()
    {
        var (owner, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var (stranger, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        await using var ownerHub = factory.CreateHubConnection(AccessToken(owner));
        await using var strangerHub = factory.CreateHubConnection(AccessToken(stranger));
        var ownerNotifications = Record(ownerHub);
        var strangerNotifications = Record(strangerHub);
        await ownerHub.StartAsync(CancellationToken);
        await strangerHub.StartAsync(CancellationToken);

        var document = await UploadAsync(owner);

        var notification = await WaitForAsync(ownerNotifications, document.Id);
        notification.ShouldBe(new DocumentStatusNotification(document.Id, DocumentStatus.Completed, FailureReason: null));

        // A connection receives messages in order, so once the stranger has the notification about its own,
        // later document, a copy of the owner's notification would already have arrived.
        var strangersDocument = await UploadAsync(stranger);
        await WaitForAsync(strangerNotifications, strangersDocument.Id);
        strangerNotifications.ShouldNotContain(received => received.DocumentId == document.Id);
        ownerNotifications.ShouldNotContain(received => received.DocumentId == strangersDocument.Id);
    }

    [Fact]
    public async Task Connection_without_an_access_token_is_rejected()
    {
        await using var hub = factory.CreateHubConnection(accessToken: null);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => hub.StartAsync(CancellationToken));

        exception.Message.ShouldContain("401");
    }

    [Fact]
    public async Task Access_token_in_the_query_string_is_accepted_only_by_the_hub()
    {
        var (owner, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/documents?access_token={AccessToken(owner)}", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<DocumentSummary> UploadAsync(HttpClient client) =>
        await (await client.UploadDocumentAsync("notes.txt", "text/plain", Encoding.UTF8.GetBytes("Meeting on 2026-11-11.")))
            .ReadAsync<DocumentSummary>();

    private static string AccessToken(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization.ShouldNotBeNull().Parameter.ShouldNotBeNull();

    private static ConcurrentQueue<DocumentStatusNotification> Record(HubConnection hub)
    {
        var notifications = new ConcurrentQueue<DocumentStatusNotification>();
        hub.On<DocumentStatusNotification>(nameof(IDocumentsClient.DocumentStatusChanged), notifications.Enqueue);

        return notifications;
    }

    private static async Task<DocumentStatusNotification> WaitForAsync(
        ConcurrentQueue<DocumentStatusNotification> notifications,
        Guid documentId)
    {
        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            if (notifications.FirstOrDefault(notification => notification.DocumentId == documentId) is { } notification)
            {
                return notification;
            }

            Stopwatch.GetElapsedTime(started).ShouldBeLessThan(_notificationTimeout, $"No notification about document {documentId}.");
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        }
    }
}
