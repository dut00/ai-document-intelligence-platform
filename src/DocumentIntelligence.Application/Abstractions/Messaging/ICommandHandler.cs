using DocumentIntelligence.Application.Abstractions.Results;

namespace DocumentIntelligence.Application.Abstractions.Messaging;

public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
