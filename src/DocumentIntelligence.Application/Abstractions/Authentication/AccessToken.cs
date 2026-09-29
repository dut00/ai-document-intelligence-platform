namespace DocumentIntelligence.Application.Abstractions.Authentication;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
