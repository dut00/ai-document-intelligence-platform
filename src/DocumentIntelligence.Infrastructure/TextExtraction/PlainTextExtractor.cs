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

    public async Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        return await reader.ReadToEndAsync(cancellationToken);
    }
}
