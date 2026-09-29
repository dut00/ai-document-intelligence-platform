using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using FluentValidation;

namespace DocumentIntelligence.Application.Abstractions.Behaviors;

/// <summary>
/// Runs every FluentValidation validator registered for the request and short-circuits
/// with a <see cref="ValidationError"/> instead of calling the handler.
/// </summary>
public static class ValidationDecorator
{
    public sealed class CommandHandler<TCommand, TResult>(
        ICommandHandler<TCommand, TResult> inner,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        public async Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken)
        {
            var error = await ValidateAsync(command, validators, cancellationToken);

            return error is null
                ? await inner.HandleAsync(command, cancellationToken)
                : error;
        }
    }

    public sealed class QueryHandler<TQuery, TResult>(
        IQueryHandler<TQuery, TResult> inner,
        IEnumerable<IValidator<TQuery>> validators)
        : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public async Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken)
        {
            var error = await ValidateAsync(query, validators, cancellationToken);

            return error is null
                ? await inner.HandleAsync(query, cancellationToken)
                : error;
        }
    }

    private static async Task<ValidationError?> ValidateAsync<TRequest>(
        TRequest request,
        IEnumerable<IValidator<TRequest>> validators,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TRequest>(request);
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return null;
        }

        var errors = failures
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

        return new ValidationError(errors);
    }
}
