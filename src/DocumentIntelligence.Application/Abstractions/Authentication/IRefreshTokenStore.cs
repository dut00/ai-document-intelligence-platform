using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Application.Abstractions.Authentication;

/// <summary>
/// Long-lived refresh tokens: stored hashed, rotated on every use, with reuse detection.
/// </summary>
public interface IRefreshTokenStore
{
    Task<IssuedRefreshToken> IssueAsync(UserId userId, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the presented token and issues its replacement. Presenting an already rotated
    /// token is treated as theft: every active token of that user is revoked.
    /// </summary>
    Task<Result<RefreshTokenRotation>> RotateAsync(string token, CancellationToken cancellationToken);

    Task RevokeAsync(string token, CancellationToken cancellationToken);
}
