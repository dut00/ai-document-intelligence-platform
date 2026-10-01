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
}
