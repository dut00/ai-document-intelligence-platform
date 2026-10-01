namespace DocumentIntelligence.Worker.Consumers;

/// <summary>
/// Which endpoints a Worker process hosts (<c>Worker:Role</c>).
/// </summary>
public enum WorkerRole
{
    /// <summary>
    /// Everything in one process, for local runs and tests. Isolated processing then shares the process
    /// with the main endpoint, so a crash there can also count against a document in isolation.
    /// </summary>
    All,

    /// <summary>
    /// New documents, deletions and faults; redelivered documents are handed on to an <see cref="Isolated"/> Worker.
    /// </summary>
    Main,

    /// <summary>
    /// Only redelivered documents, one at a time, in a process of its own: a crash there can only be the
    /// doing of the one document it was processing.
    /// </summary>
    Isolated,
}
