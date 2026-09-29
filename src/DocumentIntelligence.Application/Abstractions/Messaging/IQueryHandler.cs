using DocumentIntelligence.Application.Abstractions.Results;

namespace DocumentIntelligence.Application.Abstractions.Messaging;

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
