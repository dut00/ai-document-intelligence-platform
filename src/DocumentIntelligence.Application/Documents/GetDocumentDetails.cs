using DocumentIntelligence.Application.Abstractions.Data;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DocumentIntelligence.Application.Documents;

public sealed record GetDocumentDetailsQuery(UserId OwnerId, DocumentId DocumentId) : IQuery<DocumentDetailsResponse>;

internal sealed class GetDocumentDetailsQueryHandler(IReadDbContext readDb)
    : IQueryHandler<GetDocumentDetailsQuery, DocumentDetailsResponse>
{
    public async Task<Result<DocumentDetailsResponse>> HandleAsync(GetDocumentDetailsQuery query, CancellationToken cancellationToken)
    {
        var details = await readDb.Documents
            .Where(document => document.Id == query.DocumentId && document.OwnerId == query.OwnerId)
            .Select(document => new DocumentDetailsResponse(
                document.Id.Value,
                document.FileName.Value,
                document.ContentType.MimeType,
                document.Size.Bytes,
                document.Status,
                document.FailureReason,
                document.UploadedAt,
                document.ProcessedAt,
                document.Analysis == null ? null : DocumentAnalysisResponse.From(document.Analysis)))
            .SingleOrDefaultAsync(cancellationToken);

        return details is null ? DocumentErrors.NotFound : details;
    }
}
