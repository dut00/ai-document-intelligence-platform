using DocumentIntelligence.Api.Extensions;
using DocumentIntelligence.Api.RateLimiting;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Paging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using Microsoft.AspNetCore.Mvc;

namespace DocumentIntelligence.Api.Endpoints;

internal static class DocumentEndpoints
{
    // The file limit plus room for the multipart envelope; the exact file size is validated by the command.
    private const long MaxUploadRequestBytes = FileSize.MaxBytes + (64 * 1024);

    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents")
            .WithTags("Documents")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", UploadAsync)
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<DocumentSummary>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .RequireRateLimiting(RateLimitingExtensions.UploadPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadRequestBytes))
            .WithFormOptions(multipartBodyLengthLimit: MaxUploadRequestBytes)
            // Authentication uses a bearer token, not cookies, so there is no CSRF risk to guard against.
            .DisableAntiforgery();

        group.MapGet("/", GetDocumentsAsync)
            .Produces<PagedResponse<DocumentSummary>>()
            .ProducesValidationProblem();

        group.MapGet("/stats", GetStatsAsync)
            .Produces<DocumentStatsResponse>();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .Produces<DocumentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/{id:guid}/download", DownloadAsync)
            .Produces<Stream>(StatusCodes.Status200OK, "application/pdf", "text/plain")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", DeleteAsync)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> UploadAsync(
        IFormFile file,
        HttpContext context,
        ICommandHandler<UploadDocumentCommand, DocumentSummary> handler,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        // 202: the document is stored, its analysis follows asynchronously.
        return await HandleAsync(
            context,
            ownerId => handler.HandleAsync(
                new UploadDocumentCommand(ownerId, file.FileName, file.ContentType, file.Length, content),
                cancellationToken),
            document => TypedResults.Accepted($"/api/documents/{document.Id}", document));
    }

    private static Task<IResult> GetDocumentsAsync(
        HttpContext context,
        IQueryHandler<GetDocumentsQuery, PagedResponse<DocumentSummary>> handler,
        CancellationToken cancellationToken,
        int page = 1,
        int pageSize = 20,
        DocumentStatus? status = null) =>
        HandleAsync(
            context,
            ownerId => handler.HandleAsync(new GetDocumentsQuery(ownerId, page, pageSize, status), cancellationToken),
            documents => TypedResults.Ok(documents));

    private static Task<IResult> GetStatsAsync(
        HttpContext context,
        IQueryHandler<GetDocumentStatsQuery, DocumentStatsResponse> handler,
        CancellationToken cancellationToken) =>
        HandleAsync(
            context,
            ownerId => handler.HandleAsync(new GetDocumentStatsQuery(ownerId), cancellationToken),
            stats => TypedResults.Ok(stats));

    private static Task<IResult> GetDetailsAsync(
        Guid id,
        HttpContext context,
        IQueryHandler<GetDocumentDetailsQuery, DocumentDetailsResponse> handler,
        CancellationToken cancellationToken) =>
        HandleAsync(
            context,
            ownerId => handler.HandleAsync(new GetDocumentDetailsQuery(ownerId, new DocumentId(id)), cancellationToken),
            details => TypedResults.Ok(details));

    private static Task<IResult> DownloadAsync(
        Guid id,
        HttpContext context,
        IQueryHandler<DownloadDocumentQuery, DocumentContent> handler,
        CancellationToken cancellationToken) =>
        HandleAsync(
            context,
            ownerId => handler.HandleAsync(new DownloadDocumentQuery(ownerId, new DocumentId(id)), cancellationToken),
            file => TypedResults.Stream(file.Content, file.ContentType, file.FileName));

    private static Task<IResult> DeleteAsync(
        Guid id,
        HttpContext context,
        ICommandHandler<DeleteDocumentCommand, Unit> handler,
        CancellationToken cancellationToken) =>
        HandleAsync(
            context,
            ownerId => handler.HandleAsync(new DeleteDocumentCommand(ownerId, new DocumentId(id)), cancellationToken),
            _ => TypedResults.NoContent());

    // Resolves the caller and maps the use case's result: success via onSuccess, errors to ProblemDetails.
    private static async Task<IResult> HandleAsync<TResult>(
        HttpContext context,
        Func<UserId, Task<Result<TResult>>> handle,
        Func<TResult, IResult> onSuccess)
    {
        if (context.User.GetUserId() is not { } ownerId)
        {
            return TypedResults.Unauthorized();
        }

        var result = await handle(ownerId);

        return result.IsSuccess ? onSuccess(result.Value) : result.Error!.ToProblem();
    }
}
