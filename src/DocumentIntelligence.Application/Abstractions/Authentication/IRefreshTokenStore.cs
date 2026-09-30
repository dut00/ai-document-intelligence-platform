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
    /// token is treated as theft: every active token of that user is revoked. The exception is a
    /// token rotated moments ago whose successor is still unused: the client never received the
    /// successor, so it is rotated in the client's place.
    /// </summary>
    Task<Result<RefreshTokenRotation>> RotateAsync(string token, CancellationToken cancellationToken);

    /// <summary>
    /// Revokes the presented token and, if it was rotated moments ago, the successors that the
    /// grace period would still let it reach.
    /// </summary>
    Task RevokeAsync(string token, CancellationToken cancellationToken);
}
