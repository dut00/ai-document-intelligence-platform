namespace DocumentIntelligence.Application.Authentication;

public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
