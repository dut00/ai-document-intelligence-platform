using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Application.Abstractions.Authentication;

public sealed record RefreshTokenRotation(UserId UserId, IssuedRefreshToken NewToken);
