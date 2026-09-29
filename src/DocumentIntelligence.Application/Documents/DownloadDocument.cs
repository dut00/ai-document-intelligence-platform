using DocumentIntelligence.Application.Abstractions.Data;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DocumentIntelligence.Application.Documents;

public sealed record DownloadDocumentQuery(UserId OwnerId, DocumentId DocumentId) : IQuery<DocumentContent>;

internal sealed class DownloadDocumentQueryHandler(IReadDbContext readDb, IFileStorage storage)
    : IQueryHandler<DownloadDocumentQuery, DocumentContent>
{
    public async Task<Result<DocumentContent>> HandleAsync(DownloadDocumentQuery query, CancellationToken cancellationToken)
    {
        var file = await readDb.Documents
            .Where(document => document.Id == query.DocumentId && document.OwnerId == query.OwnerId)
            .Select(document => new { document.StorageKey, document.FileName, document.ContentType })
            .SingleOrDefaultAsync(cancellationToken);

        if (file is null)
        {
            return DocumentErrors.NotFound;
        }

        var content = await storage.OpenReadAsync(file.StorageKey, cancellationToken);

        return content is null
            ? DocumentErrors.ContentNotFound
            : new DocumentContent(content, file.FileName.Value, file.ContentType.MimeType);
    }
}
