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

    // Grace rotations can chain (T1 -> T2 -> T3 within seconds); a logout follows at most this many.
    private const int MaxRevokedSuccessors = 10;

    private const int MaxRevokeAttempts = 3;

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

        if (stored.RevokedAt is { } revokedAt && stored.ReplacedByHash is { } successorHash)
        {
            if (IsWithinGracePeriod(revokedAt, now))
            {
                var successor = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == successorHash, cancellationToken);

                // Unused successor: the response carrying it was lost (page reload, dropped connection).
                if (successor is not null && successor.IsActive(now))
                {
                    return await ReplaceAsync(successor, now, cancellationToken);
                }

                // Ended by a logout: nobody holds a live token of this chain, so there is nothing to protect.
                if (successor?.ReplacedByHash is null)
                {
                    return AuthErrors.InvalidRefreshToken;
                }

                // The successor was rotated as well, so whoever received it has moved on: the old token is a copy.
            }

            // A rotated token came back: either the client or an attacker holds a copy. Revoke the whole session.
            LogTokenReuse(logger, stored.UserId);
            await RevokeAllActiveAsync(stored.UserId, now, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        if (!stored.IsActive(now))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        return await ReplaceAsync(stored, now, cancellationToken);
    }

    public async Task RevokeAsync(string token, CancellationToken cancellationToken)
    {
        var hash = Hash(token);

        for (var attempt = 1; ; attempt++)
        {
            await MarkRevokedAsync(hash, timeProvider.GetUtcNow(), cancellationToken);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRevokeAttempts)
            {
                // A refresh rotated the token meanwhile; look again, now at its successor.
                dbContext.ChangeTracker.Clear();
            }
            catch (DbUpdateConcurrencyException)
            {
                // Still losing to refreshes: give up rather than fail the logout, which clears the cookie anyway.
                LogRevokeGaveUp(logger, MaxRevokeAttempts);
                return;
            }
        }
    }

    // A logout may carry a token that was rotated a moment ago (a refresh raced it). Within the grace
    // period that token could still reach its successor, so the chain is followed to its live token.
    // Rotated longer ago, somebody else rotated the caller's token: the same evidence of theft as in
    // RotateAsync, so the whole session is revoked.
    private async Task MarkRevokedAsync(string hash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        string? current = hash;

        for (var hop = 0; current is not null && hop <= MaxRevokedSuccessors; hop++)
        {
            var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == current, cancellationToken);
            if (stored?.RevokedAt is not { } revokedAt)
            {
                stored?.Revoke(now);
                return;
            }

            if (stored.ReplacedByHash is not null && !IsWithinGracePeriod(revokedAt, now))
            {
                LogTokenReuse(logger, stored.UserId);
                await RevokeAllActiveAsync(stored.UserId, now, cancellationToken);
                return;
            }

            current = stored.ReplacedByHash;
        }
    }

    private async Task<Result<RefreshTokenRotation>> ReplaceAsync(RefreshToken stored, DateTimeOffset now, CancellationToken cancellationToken)
    {
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

    private bool IsWithinGracePeriod(DateTimeOffset revokedAt, DateTimeOffset now) =>
        now - revokedAt <= options.Value.RefreshTokenReuseGracePeriod;

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Logout could not revoke the refresh token after {Attempts} attempts; concurrent refreshes kept rotating it")]
    private static partial void LogRevokeGaveUp(ILogger logger, int attempts);
}
