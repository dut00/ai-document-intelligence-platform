using DocumentIntelligence.Application.Abstractions.Behaviors;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Authentication;
using FluentValidation;
using NSubstitute;

namespace DocumentIntelligence.UnitTests.Application;

public sealed class ValidationDecoratorTests
{
    private readonly ICommandHandler<RegisterUserCommand, UserResponse> _inner =
        Substitute.For<ICommandHandler<RegisterUserCommand, UserResponse>>();

    [Fact]
    public async Task Invalid_command_returns_validation_error_without_calling_handler()
    {
        var decorator = CreateDecorator(new RegisterUserCommandValidator());

        var result = await decorator.HandleAsync(new RegisterUserCommand("not-an-email", "short"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        var error = result.Error.ShouldBeOfType<ValidationError>();
        error.Errors.Keys.ShouldBe(["Email", "Password"], ignoreOrder: true);
        await _inner.DidNotReceive().HandleAsync(Arg.Any<RegisterUserCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Valid_command_is_passed_to_handler()
    {
        var command = new RegisterUserCommand("user@example.com", "Password123");
        var expected = new UserResponse(Guid.NewGuid(), command.Email);
        _inner.HandleAsync(command, Arg.Any<CancellationToken>()).Returns(expected);
        var decorator = CreateDecorator(new RegisterUserCommandValidator());

        var result = await decorator.HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Fact]
    public async Task Command_without_validators_is_passed_to_handler()
    {
        var command = new RegisterUserCommand("anything", "x");
        _inner.HandleAsync(command, Arg.Any<CancellationToken>()).Returns(new UserResponse(Guid.NewGuid(), "anything"));
        var decorator = CreateDecorator();

        var result = await decorator.HandleAsync(command, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    private ValidationDecorator.CommandHandler<RegisterUserCommand, UserResponse> CreateDecorator(
        params IValidator<RegisterUserCommand>[] validators) => new(_inner, validators);
}
