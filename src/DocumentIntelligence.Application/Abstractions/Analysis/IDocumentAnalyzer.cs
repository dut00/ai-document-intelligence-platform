namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <summary>
/// Identifies what a document is and what it contains. Implementations return a result that
/// passes <c>AnalysisResultValidator</c>, or throw <see cref="UnprocessableDocumentException"/>
/// when the document cannot be analyzed. Any other exception is treated as transient and retried.
/// </summary>
public interface IDocumentAnalyzer
{
    /// <summary>
    /// The model recorded with every analysis, e.g. "claude-haiku-4-5-20251001" or "fake".
    /// </summary>
    string Model { get; }

    Task<AnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken);
}
