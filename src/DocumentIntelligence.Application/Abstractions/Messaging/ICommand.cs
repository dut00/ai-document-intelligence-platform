namespace DocumentIntelligence.Application.Abstractions.Messaging;

/// <summary>
/// A request that changes state. Handled through repositories and aggregates.
/// </summary>
public interface ICommand<TResult>;
