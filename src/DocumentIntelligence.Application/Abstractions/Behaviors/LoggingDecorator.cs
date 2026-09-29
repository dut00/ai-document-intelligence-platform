using System.Diagnostics;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using Microsoft.Extensions.Logging;

namespace DocumentIntelligence.Application.Abstractions.Behaviors;

/// <summary>
/// Logs the name, outcome and duration of every command and query. Request payloads are
/// deliberately not logged, as they may contain passwords or tokens.
/// </summary>
public static partial class LoggingDecorator
{
    public sealed class CommandHandler<TCommand, TResult>(
        ICommandHandler<TCommand, TResult> inner,
        ILogger<CommandHandler<TCommand, TResult>> logger)
        : ICommandHandler<TCommand, TResult>
        where TCommand : ICommand<TResult>
    {
        public Task<Result<TResult>> HandleAsync(TCommand command, CancellationToken cancellationToken) =>
            LogAsync(logger, typeof(TCommand).Name, () => inner.HandleAsync(command, cancellationToken));
    }

    public sealed class QueryHandler<TQuery, TResult>(
        IQueryHandler<TQuery, TResult> inner,
        ILogger<QueryHandler<TQuery, TResult>> logger)
        : IQueryHandler<TQuery, TResult>
        where TQuery : IQuery<TResult>
    {
        public Task<Result<TResult>> HandleAsync(TQuery query, CancellationToken cancellationToken) =>
            LogAsync(logger, typeof(TQuery).Name, () => inner.HandleAsync(query, cancellationToken));
    }

    private static async Task<Result<TResult>> LogAsync<TResult>(
        ILogger logger,
        string requestName,
        Func<Task<Result<TResult>>> handle)
    {
        var started = Stopwatch.GetTimestamp();
        var result = await handle();
        var elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;

        if (result.IsSuccess)
        {
            LogSucceeded(logger, requestName, elapsedMs);
        }
        else
        {
            LogFailed(logger, requestName, result.Error!.Code, elapsedMs);
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{RequestName} succeeded in {ElapsedMs:0.0} ms")]
    private static partial void LogSucceeded(ILogger logger, string requestName, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{RequestName} failed with {ErrorCode} in {ElapsedMs:0.0} ms")]
    private static partial void LogFailed(ILogger logger, string requestName, string errorCode, double elapsedMs);
}
