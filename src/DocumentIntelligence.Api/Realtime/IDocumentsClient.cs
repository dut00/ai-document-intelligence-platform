namespace DocumentIntelligence.Api.Realtime;

/// <summary>
/// Methods the server invokes on connected <see cref="DocumentsHub"/> clients.
/// </summary>
public interface IDocumentsClient
{
    Task DocumentStatusChanged(DocumentStatusNotification notification);
}
