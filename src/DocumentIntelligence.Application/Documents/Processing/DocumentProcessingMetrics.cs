using System.Diagnostics.Metrics;

namespace DocumentIntelligence.Application.Documents.Processing;

/// <summary>
/// Custom OpenTelemetry metrics of document processing, exported next to the built-in ones.
/// </summary>
internal sealed class DocumentProcessingMetrics
{
    public const string MeterName = "DocumentIntelligence.Processing";

    private readonly Counter<long> _processed;
    private readonly Histogram<double> _analysisDuration;

    public DocumentProcessingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _processed = meter.CreateCounter<long>(
            "documents.processed",
            unit: "{document}",
            description: "Documents whose processing finished, by outcome.");

        _analysisDuration = meter.CreateHistogram(
            "ai.analysis.duration",
            unit: "s",
            description: "Duration of a single AI analysis of a document's text.",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = [0.5, 1, 2, 5, 10, 20, 30, 60, 120] });
    }

    /// <summary>
    /// A document reached a final status. Skipped documents are not counted.
    /// </summary>
    /// <remarks>
    /// Recorded after SaveChanges, but in the Worker the consumer's outbox transaction commits only after
    /// the handler returns. If that commit fails, the retry processes the document again and it is
    /// counted twice. Rare, and it only skews the metric, so it is accepted.
    /// </remarks>
    public void DocumentProcessed(ProcessingOutcome outcome)
    {
        var tag = outcome switch
        {
            ProcessingOutcome.Completed => "completed",
            ProcessingOutcome.Failed => "failed",
            _ => null,
        };

        if (tag is not null)
        {
            _processed.Add(1, new KeyValuePair<string, object?>("outcome", tag));
        }
    }

    /// <param name="outcome">"success", "invalid" (the document cannot be analyzed) or "error" (a transient failure).</param>
    public void AnalysisFinished(string model, string outcome, TimeSpan duration) =>
        _analysisDuration.Record(
            duration.TotalSeconds,
            new KeyValuePair<string, object?>("model", model),
            new KeyValuePair<string, object?>("outcome", outcome));
}
