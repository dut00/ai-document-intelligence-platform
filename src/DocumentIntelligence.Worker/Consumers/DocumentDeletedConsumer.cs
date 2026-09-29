using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents;
using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// Removes a deleted document's content from object storage. Deleting a missing object
/// succeeds, so a redelivered message is harmless.
/// </summary>
public sealed partial class DocumentDeletedConsumer(IFileStorage storage, ILogger<DocumentDeletedConsumer> logger)
    : IConsumer<DocumentDeleted>
{
    public async Task Consume(ConsumeContext<DocumentDeleted> context)
    {
        var message = context.Message;

        await storage.DeleteAsync(new StorageKey(message.StorageKey), context.CancellationToken);

        LogContentDeleted(logger, message.DocumentId, message.StorageKey);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted content of document {DocumentId} at {StorageKey}")]
    private static partial void LogContentDeleted(ILogger logger, Guid documentId, string storageKey);
}
