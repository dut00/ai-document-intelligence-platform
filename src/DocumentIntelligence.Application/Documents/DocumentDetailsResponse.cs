using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.Application.Documents;

/// <summary>
/// A document with its analysis; <see cref="Analysis"/> is null until processing completes.
/// </summary>
public sealed record DocumentDetailsResponse(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DocumentStatus Status,
    string? FailureReason,
    DateTimeOffset UploadedAt,
    DateTimeOffset? ProcessedAt,
    DocumentAnalysisResponse? Analysis);
