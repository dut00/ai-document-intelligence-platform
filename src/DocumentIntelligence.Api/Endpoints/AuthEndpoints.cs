using System.Globalization;
using DocumentIntelligence.Api.Authentication;
using DocumentIntelligence.Api.Extensions;
using DocumentIntelligence.Api.RateLimiting;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Authentication;

namespace DocumentIntelligence.Api.Endpoints;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        // An endpoint has one rate-limit policy; the register one is stricter than the auth one in every window.
        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.RegisterPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .Produces<AccessTokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .Produces<AccessTokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
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
        LoginAttemptThrottle throttle,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var client = context.Connection.RemoteIpAddress;

        // Refused before the password is checked, so the answer says nothing about whether it was right.
        if (!throttle.TryBeginAttempt(client, command.Email, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

            return TypedResults.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Too many requests.",
                detail: "Too many failed sign-in attempts for this account. Try again later.");
        }

        var result = await handler.HandleAsync(command, cancellationToken);

        if (result.IsSuccess)
        {
            throttle.Succeeded(client, command.Email);
            return IssueTokens(context.Response, result.Value);
        }

        return result.Error!.ToProblem();
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
