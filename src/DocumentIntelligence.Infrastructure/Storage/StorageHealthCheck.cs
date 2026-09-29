using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Infrastructure.Storage;

/// <summary>
/// Ready when the document bucket can be listed with the configured credentials.
/// </summary>
internal sealed class StorageHealthCheck(IAmazonS3 s3, IOptions<StorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await s3.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = options.Value.BucketName, MaxKeys = 1 },
                cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // A fixed description: the exception (kept for logs) may name hosts or credentials.
            return new HealthCheckResult(context.Registration.FailureStatus, "The document bucket is not reachable.", exception);
        }
    }
}
