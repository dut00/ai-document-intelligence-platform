using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Documents.Processing;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents;
using MassTransit;

namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// Handles a <see cref="DocumentUploaded"/> (or <see cref="ProcessDocumentInIsolation"/>) message that kept
/// failing: all retries are exhausted and the message has moved to the <c>_error</c> queue, so the document
/// is marked as failed instead of staying pending.
/// </summary>
public sealed partial class DocumentUploadedFaultConsumer(
    ICommandHandler<FailDocumentProcessingCommand, ProcessingOutcome> handler,
    ILogger<DocumentUploadedFaultConsumer> logger)
    : IConsumer<Fault<DocumentUploaded>>, IConsumer<Fault<ProcessDocumentInIsolation>>
{
    // Shown to the owner, so it names no technical details; those are in the log and the error queue.
    public const string FailureReason = "Processing failed repeatedly. Please upload the document again later.";

    public Task Consume(ConsumeContext<Fault<DocumentUploaded>> context) =>
        FailAsync(new DocumentId(context.Message.Message.DocumentId), context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<Fault<ProcessDocumentInIsolation>> context) =>
        FailAsync(new DocumentId(context.Message.Message.DocumentId), context.Message, context.CancellationToken);

    private async Task FailAsync(DocumentId documentId, Fault fault, CancellationToken cancellationToken)
    {
        var errors = string.Join("; ", fault.Exceptions.Select(exception => $"{exception.ExceptionType}: {exception.Message}"));
        LogProcessingFaulted(logger, documentId, errors);

        var result = await handler.HandleAsync(new FailDocumentProcessingCommand(documentId, FailureReason), cancellationToken);
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Failing document {documentId} was rejected: {result.Error!.Description}");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Processing of document {DocumentId} failed after all retries: {Errors}")]
    private static partial void LogProcessingFaulted(ILogger logger, DocumentId documentId, string errors);
}
