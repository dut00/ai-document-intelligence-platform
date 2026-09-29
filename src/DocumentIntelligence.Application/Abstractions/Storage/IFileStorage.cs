using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.Application.Abstractions.Storage;

/// <summary>
/// Object storage for document content.
/// </summary>
public interface IFileStorage
{
    Task UploadAsync(StorageKey key, Stream content, ContentType contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Returns null when nothing is stored under <paramref name="key"/>. The caller disposes the stream.
    /// </summary>
    Task<Stream?> OpenReadAsync(StorageKey key, CancellationToken cancellationToken);

    /// <summary>
    /// Succeeds when nothing is stored under <paramref name="key"/>, so it is safe to repeat.
    /// </summary>
    Task DeleteAsync(StorageKey key, CancellationToken cancellationToken);
}
