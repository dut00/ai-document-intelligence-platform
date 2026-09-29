using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;

namespace DocumentIntelligence.Application.Authentication;

public sealed record LogoutCommand(string? RefreshToken) : ICommand<Unit>;

/// <summary>
/// Revokes the refresh token. Logging out without a token (or with an unknown one) still succeeds.
/// </summary>
internal sealed class LogoutCommandHandler(IRefreshTokenStore refreshTokens) : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Result<Unit>> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            await refreshTokens.RevokeAsync(command.RefreshToken, cancellationToken);
        }

        return Unit.Value;
    }
}
