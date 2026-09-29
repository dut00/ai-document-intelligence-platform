using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Application.Documents;

/// <summary>
/// Removes the document row. The stored content is deleted asynchronously by the Worker
/// once the <c>DocumentDeleted</c> message leaves the outbox.
/// </summary>
public sealed record DeleteDocumentCommand(UserId OwnerId, DocumentId DocumentId) : ICommand<Unit>;

internal sealed class DeleteDocumentCommandHandler(IDocumentRepository documents, IUnitOfWork unitOfWork)
    : ICommandHandler<DeleteDocumentCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(DeleteDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(command.DocumentId, cancellationToken);
        if (document is null || document.OwnerId != command.OwnerId)
        {
            return DocumentErrors.NotFound;
        }

        document.MarkForDeletion();
        documents.Remove(document);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
