using System.Text;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Domain.Documents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace DocumentIntelligence.Infrastructure.TextExtraction;

/// <summary>
/// Reads the text layer of a PDF. A scanned PDF without one yields no text (OCR is out of scope).
/// </summary>
/// <remarks>
/// PdfPig is synchronous and cannot be interrupted inside a page (or while opening the file), so each parse
/// runs on its own thread and the caller stops waiting at the timeout (the consumer slot is freed and the
/// document fails) or when it cancels. The abandoned parse ends at its next page check, keeping its parser permit until then.
/// A parse still running <see cref="StuckParseLimit"/> after it was abandoned will not end on its own, and
/// recycling the process is the only way to stop it. The Worker is then stopped gracefully: it takes no
/// new messages and lets the documents in hand finish, so no innocent document is interrupted (or, on
/// the isolated endpoint, blamed for a crash); the container then restarts. Only if the process is still
/// alive <see cref="ForcedRecycleLimit"/> later is it killed.
/// </remarks>
internal sealed partial class PdfTextExtractor(
    SemaphoreSlim parsers,
    Func<ReadOnlyMemory<byte>, int, CancellationToken, string> parse,
    TimeSpan stuckParseLimit,
    Action requestGracefulStop,
    Action forceRecycle,
    TimeProvider timeProvider,
    ILogger<PdfTextExtractor> logger)
    : ITextExtractor
{
    /// <summary>
    /// PDF parses that may run at once in this process, abandoned ones included, so that files which never
    /// finish parsing cannot pile up threads. Twice the consumer slots, so a few abandoned parses do not
    /// hold up honest documents.
    /// </summary>
    public const int MaxConcurrentParses = 8;

    /// <summary>
    /// Pages read at most. A file with thousands of pages without text (or with hostile content streams)
    /// would otherwise keep the parser busy long after the text budget stopped mattering.
    /// </summary>
    public const int MaxPages = 500;

    public static readonly TimeSpan StuckParseLimit = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Longer than a graceful Worker stop may take (its host shutdown timeout), so the forced kill only
    /// happens if that stop itself got stuck.
    /// </summary>
    public static readonly TimeSpan ForcedRecycleLimit = TimeSpan.FromMinutes(10);

    public PdfTextExtractor(TimeProvider timeProvider, IHostApplicationLifetime lifetime, ILogger<PdfTextExtractor> logger)
        : this(
            new SemaphoreSlim(MaxConcurrentParses, MaxConcurrentParses),
            Extract,
            StuckParseLimit,
            lifetime.StopApplication,
            () => Environment.FailFast("A PDF parse is stuck and the graceful stop did not finish; recycling the Worker."),
            timeProvider,
            logger)
    {
    }

    public bool CanExtract(ContentType contentType) => contentType == ContentType.Pdf;

    public async Task<string> ExtractTextAsync(Stream content, int maxCharacters, TimeSpan timeout, CancellationToken cancellationToken)
    {
        // PdfPig needs random access, storage streams are forward-only, and uploads are at most 10 MB.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);

        // Waiting for a free parser is not part of the timeout: the wait is bounded, because parsers held by
        // stuck parses get the process recycled.
        await parsers.WaitAsync(cancellationToken);

        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);

        Task<string> parsing;
        try
        {
            parsing = Task.Factory.StartNew(
                () =>
                {
                    try
                    {
                        return parse(bytes, maxCharacters, linked.Token);
                    }
                    finally
                    {
                        parsers.Release();
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
        catch
        {
            parsers.Release();
            throw;
        }

        try
        {
            return await parsing.WaitAsync(linked.Token);
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Abandoned at the timeout or by the caller (the processing deadline, a stopping consumer): either
            // way the parse may go on, and a stuck one must still get the process recycled.
            _ = WatchAbandonedParseAsync(parsing);

            if (timeoutSource.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Parsing the PDF took longer than allowed.");
            }

            throw;
        }
    }

    private async Task WatchAbandonedParseAsync(Task parsing)
    {
        var finished = await Task.WhenAny(parsing, Task.Delay(stuckParseLimit, timeProvider));
        if (finished == parsing)
        {
            // Observe the abandoned parse's outcome (normally a cancellation), so it is not reported as unobserved.
            _ = parsing.Exception;
            return;
        }

        LogStuckParse(logger, stuckParseLimit);
        requestGracefulStop();

        // Normally the process has exited long before this; the stuck thread is a background one.
        await Task.Delay(ForcedRecycleLimit, timeProvider);
        LogForcedRecycle(logger, ForcedRecycleLimit);
        forceRecycle();
    }

    private static string Extract(ReadOnlyMemory<byte> bytes, int maxCharacters, CancellationToken cancellationToken)
    {
        try
        {
            using var document = PdfDocument.Open(bytes);

            var text = new StringBuilder();
            foreach (var page in document.GetPages().Take(MaxPages))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Leading blank pages would otherwise use up the budget and leave no text at all.
                var pageText = ContentOrderTextExtractor.GetText(page);
                if (text.Length == 0)
                {
                    pageText = pageText.TrimStart();
                    if (pageText.Length == 0)
                    {
                        continue;
                    }
                }

                text.AppendLine(pageText);

                if (text.Length > maxCharacters)
                {
                    break;
                }
            }

            return text.ToString();
        }
        catch (PdfDocumentEncryptedException exception)
        {
            throw new UnprocessableDocumentException("Password-protected PDFs are not supported.", exception);
        }
        catch (Exception exception) when (exception is not (OperationCanceledException or UnprocessableDocumentException))
        {
            // PdfPig reports malformed files with a variety of exception types.
            throw new UnprocessableDocumentException("The PDF file is damaged or not a valid PDF.", exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "A PDF parse abandoned at its timeout is still running after {Limit}; stopping the Worker gracefully so it restarts")]
    private static partial void LogStuckParse(ILogger logger, TimeSpan limit);

    [LoggerMessage(Level = LogLevel.Critical, Message = "The Worker is still running {Limit} after a graceful stop was requested for a stuck PDF parse; killing it")]
    private static partial void LogForcedRecycle(ILogger logger, TimeSpan limit);
}
