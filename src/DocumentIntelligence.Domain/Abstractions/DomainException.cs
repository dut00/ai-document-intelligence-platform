namespace DocumentIntelligence.Domain.Abstractions;

/// <summary>
/// Thrown when an operation would break a domain invariant. Signals a programming error,
/// not an expected business outcome (those are returned as results by the application layer).
/// </summary>
public sealed class DomainException(string message) : Exception(message);
