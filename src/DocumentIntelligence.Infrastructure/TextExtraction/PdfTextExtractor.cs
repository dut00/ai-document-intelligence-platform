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
internal sealed class PdfTextExtractor : ITextExtractor
{
    public bool CanExtract(ContentType contentType) => contentType == ContentType.Pdf;

    public async Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken)
    {
        // PdfPig needs random access, storage streams are forward-only, and uploads are at most 10 MB.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        try
        {
            using var document = PdfDocument.Open(new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length));

            var text = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                text.AppendLine(ContentOrderTextExtractor.GetText(page));
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
