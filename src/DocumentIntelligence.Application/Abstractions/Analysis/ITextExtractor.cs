using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <summary>
/// Reads the plain text of one content type. Throws <see cref="UnprocessableDocumentException"/>
/// when the file cannot be read, e.g. a corrupt or encrypted PDF.
/// </summary>
public interface ITextExtractor
{
    bool CanExtract(ContentType contentType);

    /// <summary>
    /// Reads the text, stopping once more than <paramref name="maxCharacters"/> characters were read:
    /// the rest would be cut off anyway, and a hostile file should not be read to the end. Throws
    /// <see cref="TimeoutException"/> when reading takes longer than <paramref name="timeout"/>; waiting
    /// for a free parser does not count.
    /// </summary>
    Task<string> ExtractTextAsync(Stream content, int maxCharacters, TimeSpan timeout, CancellationToken cancellationToken);
}
