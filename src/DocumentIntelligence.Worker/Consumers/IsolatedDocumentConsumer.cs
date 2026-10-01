using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Documents.Processing;
using DocumentIntelligence.Domain.Documents;
using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// Processes a document whose delivery was interrupted, alone: its endpoint takes one message at a time
/// (<see cref="IsolatedDocumentConsumerDefinition"/>) in a Worker process that hosts nothing else
/// (<see cref="WorkerRole.Isolated"/>). A crash there can only be this document's doing, so after
/// <see cref="MaxDeliveries"/> interrupted deliveries it is failed instead of crashing Workers forever.
/// </summary>
public sealed partial class IsolatedDocumentConsumer(
    ICommandHandler<ProcessDocumentCommand, ProcessingOutcome> handler,
    ICommandHandler<FailDocumentProcessingCommand, ProcessingOutcome> failHandler,
    ILogger<IsolatedDocumentConsumer> logger)
    : IConsumer<ProcessDocumentInIsolation>
{
    public const int MaxDeliveries = 3;

    // Shown to the owner, so it names no technical details.
    public const string CrashedFailureReason = "Processing of this document stopped unexpectedly several times.";

    public async Task Consume(ConsumeContext<ProcessDocumentInIsolation> context)
    {
        var documentId = new DocumentId(context.Message.DocumentId);

        var deliveries = DeliveryCount.Of(context);
        if (deliveries >= MaxDeliveries)
        {
            LogRepeatedlyInterrupted(logger, documentId, deliveries);
            await failHandler.HandleAsync(new FailDocumentProcessingCommand(documentId, CrashedFailureReason), context.CancellationToken);
            return;
        }

        var result = await handler.HandleAsync(new ProcessDocumentCommand(documentId), context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Processing of document {documentId} was rejected: {result.Error!.Description}");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Document {DocumentId} was delivered {Deliveries} times on its own without being acknowledged; failing it")]
    private static partial void LogRepeatedlyInterrupted(ILogger logger, DocumentId documentId, int deliveries);
}
