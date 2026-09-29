namespace DocumentIntelligence.Api.Endpoints;

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
