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
public sealed class DocumentUploadedConsumer(ICommandHandler<ProcessDocumentCommand, ProcessingOutcome> handler)
    : IConsumer<DocumentUploaded>
{
    public async Task Consume(ConsumeContext<DocumentUploaded> context)
    {
        var command = new ProcessDocumentCommand(new DocumentId(context.Message.DocumentId));

        var result = await handler.HandleAsync(command, context.CancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"Processing of document {command.DocumentId} was rejected: {result.Error!.Description}");
        }
    }
}
