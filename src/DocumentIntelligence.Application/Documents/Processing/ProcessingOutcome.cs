namespace DocumentIntelligence.Application.Documents.Processing;

public enum ProcessingOutcome
{
    Completed,
    Failed,

    /// <summary>
    /// Nothing to do: the document was deleted, already processed or is being processed elsewhere.
    /// </summary>
    Skipped,
}
