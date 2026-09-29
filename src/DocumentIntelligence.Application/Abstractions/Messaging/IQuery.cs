namespace DocumentIntelligence.Application.Abstractions.Messaging;

/// <summary>
/// A read-only request. Handled by projecting straight to DTOs.
/// </summary>
public interface IQuery<TResult>;
