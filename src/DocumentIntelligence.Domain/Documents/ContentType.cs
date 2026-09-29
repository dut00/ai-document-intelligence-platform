using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents;

/// <summary>
/// The supported document formats: PDF and plain text.
/// </summary>
public sealed record ContentType
{
    public static readonly ContentType Pdf = new("application/pdf", ".pdf");
    public static readonly ContentType Txt = new("text/plain", ".txt");

    private static readonly ContentType[] _supported = [Pdf, Txt];

    private ContentType(string mimeType, string extension)
    {
        MimeType = mimeType;
        Extension = extension;
    }

    public string MimeType { get; }

    public string Extension { get; }

    public static bool TryFromMimeType(string? mimeType, out ContentType contentType)
    {
        // Ignore parameters such as "text/plain; charset=utf-8".
        var essence = mimeType?.Split(';')[0].Trim();
        contentType = _supported.FirstOrDefault(
            supported => string.Equals(supported.MimeType, essence, StringComparison.OrdinalIgnoreCase))!;

        return contentType is not null;
    }

    public static ContentType FromMimeType(string mimeType) =>
        TryFromMimeType(mimeType, out var contentType)
            ? contentType
            : throw new DomainException($"Content type '{mimeType}' is not supported.");

    public override string ToString() => MimeType;
}
