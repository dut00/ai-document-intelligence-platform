using DocumentIntelligence.Application.Abstractions.Results;

namespace DocumentIntelligence.Application.Authentication;

public static class AuthErrors
{
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "The email or password is incorrect.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid, expired or revoked.");

    public static readonly Error EmailTaken =
        Error.Conflict("Auth.EmailTaken", "An account with this email already exists.");

    public static readonly Error UserNotFound =
        Error.NotFound("Auth.UserNotFound", "The user was not found.");
}
