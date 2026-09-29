using DocumentIntelligence.Application.Abstractions.Data;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Paging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DocumentIntelligence.Application.Documents;

/// <summary>
/// The owner's documents, newest first, optionally filtered by status.
/// </summary>
public sealed record GetDocumentsQuery(UserId OwnerId, int Page, int PageSize, DocumentStatus? Status)
    : IQuery<PagedResponse<DocumentSummary>>;

public sealed class GetDocumentsQueryValidator : AbstractValidator<GetDocumentsQuery>
{
    public const int MaxPageSize = 100;

    public GetDocumentsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, MaxPageSize);
        RuleFor(query => query.Status).IsInEnum();
    }
}

internal sealed class GetDocumentsQueryHandler(IReadDbContext readDb)
    : IQueryHandler<GetDocumentsQuery, PagedResponse<DocumentSummary>>
{
    public async Task<Result<PagedResponse<DocumentSummary>>> HandleAsync(GetDocumentsQuery query, CancellationToken cancellationToken)
    {
        var documents = readDb.Documents.Where(document => document.OwnerId == query.OwnerId);

        if (query.Status is { } status)
        {
            documents = documents.Where(document => document.Status == status);
        }

        var totalCount = await documents.CountAsync(cancellationToken);
        var items = await documents
            .OrderByDescending(document => document.UploadedAt)
            .ThenByDescending(document => document.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(DocumentSummary.Projection)
            .ToListAsync(cancellationToken);

        return new PagedResponse<DocumentSummary>(items, query.Page, query.PageSize, totalCount);
    }
}
