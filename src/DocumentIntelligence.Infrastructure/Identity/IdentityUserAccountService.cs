using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Authentication;
using DocumentIntelligence.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace DocumentIntelligence.Infrastructure.Identity;

internal sealed class IdentityUserAccountService(UserManager<ApplicationUser> userManager) : IUserAccountService
{
    public async Task<Result<UserAccount>> RegisterAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = new ApplicationUser { Id = Guid.CreateVersion7(), UserName = email, Email = email };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            return ToAccount(user);
        }

        if (result.Errors.Any(error => error.Code is "DuplicateUserName" or "DuplicateEmail"))
        {
            return AuthErrors.EmailTaken;
        }

        // Identity's password rules are stricter than the request validator (digits, casing), so report them per field.
        var errors = result.Errors
            .GroupBy(error => error.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" : "Email")
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray());

        return new ValidationError(errors);
    }

    public async Task<Result<UserAccount>> ValidateCredentialsAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);

        // The same error for unknown users, wrong passwords and locked accounts avoids revealing which emails exist.
        if (user is null || await userManager.IsLockedOutAsync(user))
        {
            return AuthErrors.InvalidCredentials;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            return AuthErrors.InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return ToAccount(user);
    }

    public async Task<UserAccount?> FindByIdAsync(UserId id, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(id.ToString());

        return user is null ? null : ToAccount(user);
    }

    private static UserAccount ToAccount(ApplicationUser user) => new(new UserId(user.Id), user.Email!);
}
