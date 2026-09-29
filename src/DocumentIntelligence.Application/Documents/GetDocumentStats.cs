using DocumentIntelligence.Application.Abstractions.Data;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace DocumentIntelligence.Application.Documents;

/// <summary>
/// Document counts per status for the owner's dashboard.
/// </summary>
public sealed record GetDocumentStatsQuery(UserId OwnerId) : IQuery<DocumentStatsResponse>;

internal sealed class GetDocumentStatsQueryHandler(IReadDbContext readDb)
    : IQueryHandler<GetDocumentStatsQuery, DocumentStatsResponse>
{
    public async Task<Result<DocumentStatsResponse>> HandleAsync(GetDocumentStatsQuery query, CancellationToken cancellationToken)
    {
        var counts = await readDb.Documents
            .Where(document => document.OwnerId == query.OwnerId)
            .GroupBy(document => document.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Status, group => group.Count, cancellationToken);

        return new DocumentStatsResponse(
            counts.Values.Sum(),
            counts.GetValueOrDefault(DocumentStatus.Pending),
            counts.GetValueOrDefault(DocumentStatus.Processing),
            counts.GetValueOrDefault(DocumentStatus.Completed),
            counts.GetValueOrDefault(DocumentStatus.Failed));
    }
}
