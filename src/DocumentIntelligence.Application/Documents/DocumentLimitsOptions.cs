using System.ComponentModel.DataAnnotations;

namespace DocumentIntelligence.Application.Documents;

/// <summary>
/// Caps that bound what one user, or everyone together, can make the system store and spend on AI.
/// Accounts are free to create, so a per-user limit alone does not bound the total.
/// </summary>
public sealed class DocumentLimitsOptions
{
    public const string SectionName = "Documents";

    /// <summary>
    /// Documents one user may keep; uploading more requires deleting some first.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int MaxDocumentsPerUser { get; init; } = 200;

    /// <summary>
    /// Documents analyzed by the AI across all users in any 24 hours; further documents fail with a
    /// message asking to upload them again later.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DailyAnalysisLimit { get; init; } = 500;

    /// <summary>
    /// Documents analyzed for one user in any 24 hours, well below <see cref="DailyAnalysisLimit"/>, so
    /// one account cannot use up everyone's allowance.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int DailyAnalysisLimitPerUser { get; init; } = 50;
}
