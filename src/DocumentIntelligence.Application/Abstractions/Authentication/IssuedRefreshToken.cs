namespace DocumentIntelligence.Application.Abstractions.Authentication;

public sealed record IssuedRefreshToken(string Token, DateTimeOffset ExpiresAt);
