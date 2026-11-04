namespace DocumentIntelligence.Infrastructure.Persistence;

/// <summary>
/// The AI analysis of one document, kept after the document is deleted (see <c>IAnalysisUsageLog</c>).
/// The daily limits cap them at a few hundred rows a day, so they are not pruned.
/// </summary>
internal sealed class AnalysisUsage
{
    public Guid Id { get; init; }

    public Guid OwnerId { get; init; }

    public Guid DocumentId { get; init; }

    public DateTimeOffset AnalyzedAt { get; init; }

    /// <summary>
    /// How many processing attempts may have cost money for this document: those that reached the AI step,
    /// minus those that got no answer because the service was unavailable.
    /// </summary>
    public int Attempts { get; init; }
}
