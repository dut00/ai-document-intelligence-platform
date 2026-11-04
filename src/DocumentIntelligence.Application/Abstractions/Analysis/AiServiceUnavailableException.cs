namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <summary>
/// A transient failure that excludes a generated answer as far as the client can tell: the service refused
/// the first request, or the connection failed before it was sent. It is retried like any other transient
/// failure, but does not use up an analysis attempt.
/// </summary>
public sealed class AiServiceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
