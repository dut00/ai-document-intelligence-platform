using DocumentIntelligence.Application.Abstractions.Authentication;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using FluentValidation;

namespace DocumentIntelligence.Application.Authentication;

public sealed record RegisterUserCommand(string Email, string Password) : ICommand<UserResponse>;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public const int MinPasswordLength = 8;

    public RegisterUserCommandValidator()
    {
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(MinPasswordLength).MaximumLength(128);
    }
}

internal sealed class RegisterUserCommandHandler(IUserAccountService accounts)
    : ICommandHandler<RegisterUserCommand, UserResponse>
{
    public async Task<Result<UserResponse>> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        var result = await accounts.RegisterAsync(command.Email.Trim(), command.Password, cancellationToken);

        return result.IsSuccess
            ? new UserResponse(result.Value.Id.Value, result.Value.Email)
            : result.Error!;
    }
}
