using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.Application.Abstractions.Analysis;

/// <summary>
/// Reads the plain text of one content type. Throws <see cref="UnprocessableDocumentException"/>
/// when the file cannot be read, e.g. a corrupt or encrypted PDF.
/// </summary>
public interface ITextExtractor
{
    bool CanExtract(ContentType contentType);

    Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken);
}
