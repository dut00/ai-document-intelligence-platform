using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.Application.Abstractions.Authentication;

/// <summary>
/// User accounts (backed by ASP.NET Core Identity).
/// </summary>
public interface IUserAccountService
{
    Task<Result<UserAccount>> RegisterAsync(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the password and applies lockout after repeated failures.
    /// </summary>
    Task<Result<UserAccount>> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken);

    Task<UserAccount?> FindByIdAsync(UserId id, CancellationToken cancellationToken);
}
