using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Domain.Documents;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Infrastructure.Storage;

internal sealed class S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options) : IFileStorage
{
    private string BucketName => options.Value.BucketName;

    public async Task UploadAsync(StorageKey key, Stream content, ContentType contentType, CancellationToken cancellationToken)
    {
        var request = new PutObjectRequest
        {
            BucketName = BucketName,
            Key = key.Value,
            InputStream = content,
            ContentType = contentType.MimeType,
            AutoCloseStream = false,
        };

        await s3.PutObjectAsync(request, cancellationToken);
    }

    public async Task<Stream?> OpenReadAsync(StorageKey key, CancellationToken cancellationToken)
    {
        try
        {
            var response = await s3.GetObjectAsync(BucketName, key.Value, cancellationToken);

            // Disposing the response stream also releases the underlying HTTP response.
            return response.ResponseStream;
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(StorageKey key, CancellationToken cancellationToken) =>
        await s3.DeleteObjectAsync(BucketName, key.Value, cancellationToken);
}
