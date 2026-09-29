using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Authentication;
using DocumentIntelligence.Domain.Users;
using DocumentIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Infrastructure.Authentication;

internal sealed partial class RefreshTokenStore(
    ApplicationDbContext dbContext,
    IOptions<JwtOptions> options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenStore> logger)
    : IRefreshTokenStore
{
    private const int TokenSizeInBytes = 64;

    public async Task<IssuedRefreshToken> IssueAsync(UserId userId, CancellationToken cancellationToken)
    {
        var (token, stored) = CreateToken(userId.Value, timeProvider.GetUtcNow());

        dbContext.RefreshTokens.Add(stored);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken(token, stored.ExpiresAt);
    }

    public async Task<Result<RefreshTokenRotation>> RotateAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (stored is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var now = timeProvider.GetUtcNow();

        if (stored.RevokedAt is not null && stored.ReplacedByHash is not null)
        {
            // A rotated token came back: either the client or an attacker holds a copy. Revoke the whole session.
            LogTokenReuse(logger, stored.UserId);
            await RevokeAllActiveAsync(stored.UserId, now, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        if (!stored.IsActive(now))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var (newToken, replacement) = CreateToken(stored.UserId, now);
        stored.Revoke(now, replacement.TokenHash);
        dbContext.RefreshTokens.Add(replacement);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A parallel request rotated the same token first; only one of them may win.
            return AuthErrors.InvalidRefreshToken;
        }

        return new RefreshTokenRotation(new UserId(stored.UserId), new IssuedRefreshToken(newToken, replacement.ExpiresAt));
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);
        var now = timeProvider.GetUtcNow();

        await dbContext.RefreshTokens
            .Where(t => t.TokenHash == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    private Task RevokeAllActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);

    private (string Token, RefreshToken Stored) CreateToken(Guid userId, DateTimeOffset now)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));
        var stored = RefreshToken.Create(userId, Hash(token), now, options.Value.RefreshTokenLifetime);

        return (token, stored);
    }

    // Tokens are 512 random bits, so a fast hash is enough; a slow password hash would add nothing.
    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}; all sessions revoked")]
    private static partial void LogTokenReuse(ILogger logger, Guid userId);
}
