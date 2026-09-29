using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using FluentValidation;

namespace DocumentIntelligence.Application.Authentication;

public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokens>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty();
        RuleFor(command => command.Password).NotEmpty();
    }
}

internal sealed class LoginCommandHandler(
    IUserAccountService accounts,
    IAccessTokenIssuer accessTokens,
    IRefreshTokenStore refreshTokens)
    : ICommandHandler<LoginCommand, AuthTokens>
{
    public async Task<Result<AuthTokens>> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var account = await accounts.ValidateCredentialsAsync(command.Email.Trim(), command.Password, cancellationToken);
        if (account.IsFailure)
        {
            return account.Error!;
        }

        var accessToken = accessTokens.Issue(account.Value);
        var refreshToken = await refreshTokens.IssueAsync(account.Value.Id, cancellationToken);

        return new AuthTokens(accessToken.Token, accessToken.ExpiresAt, refreshToken.Token, refreshToken.ExpiresAt);
    }
}
