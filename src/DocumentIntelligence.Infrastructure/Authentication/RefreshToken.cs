namespace DocumentIntelligence.Infrastructure.Authentication;

/// <summary>
/// A stored refresh token. Only the SHA-256 hash of the token is persisted.
/// </summary>
public sealed class RefreshToken
{
    private RefreshToken(Guid id, Guid userId, string tokenHash, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Id = id;
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    /// <summary>
    /// Hash of the token that replaced this one on rotation. A revoked token with a replacement
    /// that is presented again indicates reuse of a stolen token.
    /// </summary>
    public string? ReplacedByHash { get; private set; }

    /// <summary>
    /// Maps to PostgreSQL's xmin; makes concurrent rotations of the same token conflict.
    /// </summary>
    public uint Version { get; private set; }

    public static RefreshToken Create(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), userId, tokenHash, now, now + lifetime);

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    public void Revoke(DateTimeOffset now, string? replacedByHash = null)
    {
        RevokedAt = now;
        ReplacedByHash = replacedByHash;
    }
}
