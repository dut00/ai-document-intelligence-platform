using System.Text;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Domain.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace DocumentIntelligence.Infrastructure.TextExtraction;

/// <summary>
/// Reads the text layer of a PDF. A scanned PDF without one yields no text (OCR is out of scope).
/// </summary>
internal sealed class PdfTextExtractor(SemaphoreSlim parsers) : ITextExtractor
{
    /// <summary>
    /// PDF parses that may run at once in this process, abandoned ones included (see below), so that
    /// files which never finish parsing cannot pile up threads until the Worker's CPU is exhausted.
    /// </summary>
    public const int MaxConcurrentParses = 4;

    /// <summary>
    /// Pages read at most. A file with thousands of pages without text (or with hostile content streams)
    /// would otherwise keep the parser busy long after the text budget stopped mattering.
    /// </summary>
    public const int MaxPages = 500;

    public PdfTextExtractor()
        : this(new SemaphoreSlim(MaxConcurrentParses, MaxConcurrentParses))
    {
    }

    public bool CanExtract(ContentType contentType) => contentType == ContentType.Pdf;

    public async Task<string> ExtractTextAsync(Stream content, int maxCharacters, CancellationToken cancellationToken)
    {
        // PdfPig needs random access, storage streams are forward-only, and uploads are at most 10 MB.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);

        // PdfPig is synchronous and cannot be interrupted inside a page (or while opening the file), so it
        // runs on its own thread and the caller stops waiting once the token fires: the consumer slot is
        // freed and the document fails. The abandoned parse ends at its next page check, and keeps its
        // permit until then. With every permit held, documents wait (a transient error, retried later)
        // rather than start yet another parse.
        if (!parsers.Wait(0))
        {
            throw new InvalidOperationException("All PDF parsers are busy; the document will be retried.");
        }

        var parsing = Task.Factory.StartNew(
            () =>
            {
                try
                {
                    return Extract(bytes, maxCharacters, cancellationToken);
                }
                finally
                {
                    parsers.Release();
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        return await parsing.WaitAsync(cancellationToken);
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
}
