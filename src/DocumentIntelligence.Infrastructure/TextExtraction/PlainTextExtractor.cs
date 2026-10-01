using System.Text;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.Infrastructure.TextExtraction;

/// <summary>
/// Reads a text file as UTF-8, honouring a byte order mark for UTF-16/32.
/// </summary>
internal sealed class PlainTextExtractor : ITextExtractor
{
    public bool CanExtract(ContentType contentType) => contentType == ContentType.Txt;

    public async Task<string> ExtractTextAsync(Stream content, int maxCharacters, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        // One character more than the limit tells the caller the text was cut. Leading whitespace is not
        // counted: it would otherwise use up the budget and leave no text at all. Read in chunks rather
        // than with Peek, which gives up on a network stream that returns a short read.
        var text = new StringBuilder();
        var chunk = new char[4096];
        int read;
        while (text.Length <= maxCharacters && (read = await reader.ReadAsync(chunk, cancellationToken)) > 0)
        {
            var span = chunk.AsSpan(0, read);
            if (text.Length == 0)
            {
                span = span.TrimStart();
            }

            text.Append(span[..Math.Min(span.Length, maxCharacters + 1 - text.Length)]);
        }

        return text.ToString();
    }
}
