using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;

namespace DocumentIntelligence.Application.Authentication;

public sealed record RefreshTokenCommand(string RefreshToken) : ICommand<AuthTokens>;

internal sealed class RefreshTokenCommandHandler(
    IUserAccountService accounts,
    IAccessTokenIssuer accessTokens,
    IRefreshTokenStore refreshTokens)
    : ICommandHandler<RefreshTokenCommand, AuthTokens>
{
    public async Task<Result<AuthTokens>> HandleAsync(RefreshTokenCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var rotation = await refreshTokens.RotateAsync(command.RefreshToken, cancellationToken);
        if (rotation.IsFailure)
        {
            return rotation.Error!;
        }

        var account = await accounts.FindByIdAsync(rotation.Value.UserId, cancellationToken);
        if (account is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var accessToken = accessTokens.Issue(account);
        var refreshToken = rotation.Value.NewToken;

        return new AuthTokens(accessToken.Token, accessToken.ExpiresAt, refreshToken.Token, refreshToken.ExpiresAt);
    }
}
