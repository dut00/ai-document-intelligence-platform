using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using FluentValidation;

namespace DocumentIntelligence.Application.Documents.Processing;

/// <summary>
/// Gives up on a document whose processing kept failing, e.g. after all retries were exhausted.
/// </summary>
public sealed record FailDocumentProcessingCommand(DocumentId DocumentId, string Reason) : ICommand<ProcessingOutcome>;

public sealed class FailDocumentProcessingCommandValidator : AbstractValidator<FailDocumentProcessingCommand>
{
    public FailDocumentProcessingCommandValidator() =>
        RuleFor(command => command.Reason).NotEmpty();
}

internal sealed class FailDocumentProcessingCommandHandler(
    IDocumentRepository documents,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    DocumentProcessingMetrics metrics)
    : ICommandHandler<FailDocumentProcessingCommand, ProcessingOutcome>
{
    public async Task<Result<ProcessingOutcome>> HandleAsync(FailDocumentProcessingCommand command, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(command.DocumentId, cancellationToken);
        if (document is null || document.Status is DocumentStatus.Completed or DocumentStatus.Failed)
        {
            return ProcessingOutcome.Skipped;
        }

        document.Fail(command.Reason, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        metrics.DocumentProcessed(ProcessingOutcome.Failed);

        return ProcessingOutcome.Failed;
    }
}
