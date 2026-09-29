using System.Net;
using Amazon.S3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Infrastructure.Storage;

/// <summary>
/// Creates the document bucket on startup, so a fresh storage container needs no manual setup.
/// </summary>
internal sealed partial class StorageBucketInitializer(
    IAmazonS3 s3,
    IOptions<StorageOptions> options,
    ILogger<StorageBucketInitializer> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bucketName = options.Value.BucketName;

        try
        {
            await s3.PutBucketAsync(bucketName, cancellationToken);
            LogBucketCreated(logger, bucketName);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode == HttpStatusCode.Conflict)
        {
            // BucketAlreadyOwnedByYou / BucketAlreadyExists: nothing to do.
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Created storage bucket {BucketName}")]
    private static partial void LogBucketCreated(ILogger logger, string bucketName);
}
