using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Application.Abstractions.Authentication;

public sealed record UserAccount(UserId Id, string Email);
