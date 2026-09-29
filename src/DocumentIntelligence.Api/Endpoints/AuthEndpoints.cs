using DocumentIntelligence.Api.Authentication;
using DocumentIntelligence.Api.Extensions;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Authentication;

namespace DocumentIntelligence.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .Produces<AccessTokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", GetCurrentUserAsync)
            .RequireAuthorization()
            .Produces<UserResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterUserCommand command,
        ICommandHandler<RegisterUserCommand, UserResponse> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? TypedResults.Created("/api/auth/me", result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<IResult> LoginAsync(
        LoginCommand command,
        ICommandHandler<LoginCommand, AuthTokens> handler,
        HttpResponse response,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? IssueTokens(response, result.Value)
            : result.Error!.ToProblem();
    }

    private static async Task<IResult> RefreshAsync(
        HttpRequest request,
        HttpResponse response,
        ICommandHandler<RefreshTokenCommand, AuthTokens> handler,
        CancellationToken cancellationToken)
    {
        var token = RefreshTokenCookie.Read(request);
        if (string.IsNullOrEmpty(token))
        {
            return AuthErrors.InvalidRefreshToken.ToProblem();
        }

        var result = await handler.HandleAsync(new RefreshTokenCommand(token), cancellationToken);
        if (result.IsFailure)
        {
            RefreshTokenCookie.Delete(response);
            return result.Error!.ToProblem();
        }

        return IssueTokens(response, result.Value);
    }

    private static async Task<IResult> LogoutAsync(
        HttpRequest request,
        HttpResponse response,
        ICommandHandler<LogoutCommand, Unit> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new LogoutCommand(RefreshTokenCookie.Read(request)), cancellationToken);
        RefreshTokenCookie.Delete(response);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext context,
        IQueryHandler<GetCurrentUserQuery, UserResponse> handler,
        CancellationToken cancellationToken)
    {
        if (context.User.GetUserId() is not { } userId)
        {
            return TypedResults.Unauthorized();
        }

        var result = await handler.HandleAsync(new GetCurrentUserQuery(userId), cancellationToken);

        return result.IsSuccess ? TypedResults.Ok(result.Value) : result.Error!.ToProblem();
    }

    private static IResult IssueTokens(HttpResponse response, AuthTokens tokens)
    {
        RefreshTokenCookie.Write(response, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);

        return TypedResults.Ok(new AccessTokenResponse(tokens.AccessToken, tokens.AccessTokenExpiresAt));
    }
}
