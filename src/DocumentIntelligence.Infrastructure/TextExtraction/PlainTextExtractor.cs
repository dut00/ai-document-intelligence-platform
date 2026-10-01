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

        // Leading whitespace would otherwise use up the budget and leave no text at all.
        while (reader.Peek() is var next && next >= 0 && char.IsWhiteSpace((char)next))
        {
            cancellationToken.ThrowIfCancellationRequested();
            reader.Read();
        }

        // One character more than the limit tells the caller the text was cut.
        var buffer = new char[maxCharacters + 1];
        var length = await reader.ReadBlockAsync(buffer, cancellationToken);

        return new string(buffer, 0, length);
    }
}
