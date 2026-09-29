using System.ComponentModel.DataAnnotations;

namespace DocumentIntelligence.Infrastructure.Storage;

/// <summary>
/// Connection to an S3-compatible object store (SeaweedFS locally).
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public Uri ServiceUrl { get; init; } = null!;

    [Required]
    public string AccessKey { get; init; } = string.Empty;

    [Required]
    public string SecretKey { get; init; } = string.Empty;

    [Required]
    public string BucketName { get; init; } = string.Empty;
}
