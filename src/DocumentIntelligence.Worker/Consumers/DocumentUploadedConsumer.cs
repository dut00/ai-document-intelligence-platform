using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Documents.Processing;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents;
using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// Processes an uploaded document. A thrown exception means a transient failure: the message is
/// retried, and once the retries are exhausted <see cref="DocumentUploadedFaultConsumer"/> fails the document.
/// </summary>
/// <remarks>
/// A document that crashes the Worker itself (e.g. a hostile PDF exhausting memory) never reaches the
/// retry policy: the process dies and RabbitMQ redelivers every message the Worker held, the culprit and
/// the innocent ones processed next to it alike. A redelivered message is therefore not processed here
/// but handed to <see cref="IsolatedDocumentConsumer"/>, which processes one document at a time in a
/// process of its own, so only a document that crashes the Worker on its own is eventually failed.
/// </remarks>
public sealed partial class DocumentUploadedConsumer(
    ICommandHandler<ProcessDocumentCommand, ProcessingOutcome> handler,
    ILogger<DocumentUploadedConsumer> logger)
    : IConsumer<DocumentUploaded>
{
    public async Task Consume(ConsumeContext<DocumentUploaded> context)
    {
        var documentId = new DocumentId(context.Message.DocumentId);

        var deliveries = DeliveryCount.Of(context);
        if (deliveries > 0)
        {
            LogMovedToIsolation(logger, documentId, deliveries);

            // Through the outbox: sent once this consumer commits, and the original is acknowledged then.
            await context.Send(IsolatedDocumentConsumerDefinition.QueueAddress, new ProcessDocumentInIsolation(documentId.Value));
            return;
        }

        var result = await handler.HandleAsync(new ProcessDocumentCommand(documentId), context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Processing of document {documentId} was rejected: {result.Error!.Description}");
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Document {DocumentId} was redelivered after {Deliveries} interrupted deliveries; processing it in isolation")]
    private static partial void LogMovedToIsolation(ILogger logger, DocumentId documentId, int deliveries);
}
