using System.Globalization;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Analysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Application.Documents.Processing;

/// <summary>
/// Extracts the text of an uploaded document, analyzes it and stores the analysis. Safe to repeat:
/// a document that is gone, finished or being processed elsewhere is skipped.
/// </summary>
/// <remarks>
/// Permanent problems (no text, unreadable file, invalid AI response) fail the document and return
/// <see cref="ProcessingOutcome.Failed"/>. Any other exception, including a concurrency conflict on
/// save, propagates so the caller can retry.
/// </remarks>
public sealed record ProcessDocumentCommand(DocumentId DocumentId) : ICommand<ProcessingOutcome>;

internal sealed partial class ProcessDocumentCommandHandler(
    IDocumentRepository documents,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    IEnumerable<ITextExtractor> textExtractors,
    IDocumentAnalyzer analyzer,
    IDateInsightsService dateInsights,
    IAnalysisUsageLog usageLog,
    IOptions<DocumentLimitsOptions> limits,
    TimeProvider timeProvider,
    DocumentProcessingMetrics metrics,
    ILogger<ProcessDocumentCommandHandler> logger)
    : ICommandHandler<ProcessDocumentCommand, ProcessingOutcome>
{
    /// <summary>
    /// Roughly 15k tokens: bounds the cost and latency of a single analysis.
    /// </summary>
    public const int MaxAnalyzedCharacters = 60_000;

    /// <summary>
    /// Reading the text of an honest 10 MB file takes seconds; a file that takes longer is treated as
    /// hostile and failed rather than retried.
    /// </summary>
    public static readonly TimeSpan ExtractionTimeout = TimeSpan.FromMinutes(1);

    public const string DailyLimitReachedReason =
        "The daily analysis limit has been reached. Please upload the document again tomorrow.";

    public const string UserDailyLimitReachedReason =
        "You have reached your daily analysis limit. Please upload the document again tomorrow.";

    /// <summary>
    /// A document left in processing this long, e.g. by a crashed worker, may be picked up again.
    /// </summary>
    public static readonly TimeSpan StaleProcessingTimeout = TimeSpan.FromMinutes(15);

    public async Task<Result<ProcessingOutcome>> HandleAsync(ProcessDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = await documents.GetByIdAsync(command.DocumentId, cancellationToken);
        if (document is null)
        {
            LogSkippedMissing(logger, command.DocumentId);
            return ProcessingOutcome.Skipped;
        }

        if (!document.CanStartProcessing(timeProvider.GetUtcNow(), StaleProcessingTimeout))
        {
            LogSkippedStatus(logger, document.Id, document.Status);
            return ProcessingOutcome.Skipped;
        }

        // Deliberately not saved yet: the Worker runs this inside one transaction, and an early update
        // would lock the row for the whole AI call (blocking e.g. a delete). The row version check on
        // the final save still rejects the outcome if the document changed or was deleted meanwhile.
        document.StartProcessing(timeProvider.GetUtcNow(), StaleProcessingTimeout);

        DocumentAnalysis analysis;
        try
        {
            var text = await ExtractTextAsync(document, cancellationToken);
            await ReserveAnalysisAsync(document, cancellationToken);
            var result = await AnalyzeAsync(text, cancellationToken);
            analysis = await CreateAnalysisAsync(document.Id, result, cancellationToken);
        }
        catch (UnprocessableDocumentException exception)
        {
            LogUnprocessable(logger, exception, document.Id, exception.Message);
            document.Fail(exception.Message, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
            metrics.DocumentProcessed(ProcessingOutcome.Failed);

            return ProcessingOutcome.Failed;
        }

        document.Complete(analysis, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        metrics.DocumentProcessed(ProcessingOutcome.Completed);

        return ProcessingOutcome.Completed;
    }

    private async Task<string> ExtractTextAsync(Document document, CancellationToken cancellationToken)
    {
        var extractor = textExtractors.FirstOrDefault(candidate => candidate.CanExtract(document.ContentType))
            ?? throw new UnprocessableDocumentException($"Files of type {document.ContentType} cannot be processed.");

        await using var content = await storage.OpenReadAsync(document.StorageKey, cancellationToken)
            ?? throw new UnprocessableDocumentException("The file content is missing.");

        string text;
        using (var timeout = new CancellationTokenSource(ExtractionTimeout, timeProvider))
        using (var extraction = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token))
        {
            try
            {
                text = (await extractor.ExtractTextAsync(content, MaxAnalyzedCharacters, extraction.Token)).Trim();
            }
            // Only our own timeout is permanent; any other cancellation (e.g. a storage request timing out)
            // is a transient failure and propagates, so the message is retried.
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new UnprocessableDocumentException("Reading the document took too long.");
            }
        }

        if (text.Length == 0)
        {
            throw new UnprocessableDocumentException(
                "The document contains no extractable text. Scanned PDFs without a text layer are not supported.");
        }

        if (text.Length > MaxAnalyzedCharacters)
        {
            LogTruncated(logger, document.Id, MaxAnalyzedCharacters);

            // Never cut a surrogate pair in half.
            var length = char.IsHighSurrogate(text[MaxAnalyzedCharacters - 1]) ? MaxAnalyzedCharacters - 1 : MaxAnalyzedCharacters;
            text = text[..length];
        }

        return text;
    }

    // Bounds the AI cost: overall, because accounts are free, and per user, so one account cannot use up
    // everyone's allowance. Counted from the usage log, which deleting documents does not shrink.
    // Concurrent Workers can overshoot by a few documents; the limits do not need to be exact.
    private async Task ReserveAnalysisAsync(Document document, CancellationToken cancellationToken)
    {
        // A retry or redelivery of a document that was already counted (and perhaps already paid for).
        if (await usageLog.IsRecordedAsync(document.Id, cancellationToken))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var since = now.AddDays(-1);

        if (await usageLog.CountForOwnerSinceAsync(document.OwnerId, since, cancellationToken) >= limits.Value.DailyAnalysisLimitPerUser)
        {
            LogDailyLimitReached(logger, "per-user", limits.Value.DailyAnalysisLimitPerUser);
            throw new UnprocessableDocumentException(UserDailyLimitReachedReason);
        }

        if (await usageLog.CountSinceAsync(since, cancellationToken) >= limits.Value.DailyAnalysisLimit)
        {
            LogDailyLimitReached(logger, "overall", limits.Value.DailyAnalysisLimit);
            throw new UnprocessableDocumentException(DailyLimitReachedReason);
        }

        // Committed now, before the AI is called: if this attempt later rolls back (the document was deleted
        // meanwhile, or a transient error), the analysis has still been paid for and must still count.
        await usageLog.RecordAsync(document.OwnerId, document.Id, now, cancellationToken);
    }

    private async Task<AnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken)
    {
        var started = timeProvider.GetTimestamp();
        var outcome = "error";

        try
        {
            var result = await analyzer.AnalyzeAsync(text, cancellationToken);
            outcome = "success";
            return result;
        }
        catch (UnprocessableDocumentException)
        {
            outcome = "invalid";
            throw;
        }
        finally
        {
            metrics.AnalysisFinished(analyzer.Model, outcome, timeProvider.GetElapsedTime(started));
        }
    }

    // The result has passed AnalysisResultValidator, so parsing cannot fail here.
    private async Task<DocumentAnalysis> CreateAnalysisAsync(DocumentId documentId, AnalysisResult result, CancellationToken cancellationToken)
    {
        var dates = result.ImportantDates
            .Select(date => new ImportantDate(
                DateOnly.ParseExact(date.Date, AnalysisResult.DateFormat, CultureInfo.InvariantCulture),
                Enum.Parse<ImportantDateType>(date.Type),
                date.Description))
            .ToList();

        var checkedDates = await dateInsights.AddCalendarChecksAsync(dates, cancellationToken);

        return DocumentAnalysis.Create(
            documentId,
            result.DocumentType,
            result.Summary,
            result.Entities.Select(entity => new ExtractedEntity(Enum.Parse<EntityType>(entity.Type), entity.Name)),
            checkedDates,
            result.FinancialInformation.Select(item => new FinancialItem(item.Description, new Money(item.Amount, item.Currency))),
            result.PotentialRisks.Select(risk => new Risk(Enum.Parse<RiskSeverity>(risk.Severity), risk.Description)),
            analyzer.Model,
            timeProvider.GetUtcNow());
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping document {DocumentId}: it no longer exists")]
    private static partial void LogSkippedMissing(ILogger logger, DocumentId documentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipping document {DocumentId} in status {Status}")]
    private static partial void LogSkippedStatus(ILogger logger, DocumentId documentId, DocumentStatus status);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Document {DocumentId} cannot be processed: {Reason}")]
    private static partial void LogUnprocessable(ILogger logger, Exception exception, DocumentId documentId, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The {Scope} daily limit of {Limit} analyses has been reached; failing the document")]
    private static partial void LogDailyLimitReached(ILogger logger, string scope, int limit);

    [LoggerMessage(Level = LogLevel.Information, Message = "Text of document {DocumentId} truncated to {MaxLength} characters")]
    private static partial void LogTruncated(ILogger logger, DocumentId documentId, int maxLength);
}
