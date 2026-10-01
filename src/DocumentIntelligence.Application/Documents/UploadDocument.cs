using DocumentIntelligence.Application.Abstractions.Messaging;
using DocumentIntelligence.Application.Abstractions.Results;
using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Application.Documents;

/// <summary>
/// <paramref name="Content"/> must be seekable: validation reads the file signature and rewinds.
/// </summary>
public sealed record UploadDocumentCommand(
    UserId OwnerId,
    string FileName,
    string? ContentType,
    long Length,
    Stream Content)
    : ICommand<DocumentSummary>;

public sealed class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    // Enough of a text file to tell it apart from a binary one.
    private const int TextSampleSize = 8 * 1024;

    private static readonly byte[] _pdfSignature = "%PDF-"u8.ToArray();

    public UploadDocumentCommandValidator()
    {
        // Each rule relies on the previous ones, e.g. the signature check needs a supported content type.
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(command => command.FileName)
            .NotEmpty()
            .MaximumLength(FileName.MaxLength);

        RuleFor(command => command.Length)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(FileSize.MaxBytes).WithMessage($"The file must not exceed {FileSize.MaxBytes / 1024 / 1024} MB.");

        RuleFor(command => command.ContentType)
            .Must(contentType => ContentType.TryFromMimeType(contentType, out _))
            .WithMessage("Only PDF and plain text files are supported.");

        RuleFor(command => command.FileName)
            .Must((command, fileName) => HasExtensionOf(fileName, ContentType.FromMimeType(command.ContentType!)))
            .WithMessage("The file extension does not match the content type.");

        RuleFor(command => command.Content)
            .MustAsync((command, content, cancellationToken) =>
                HasValidSignatureAsync(ContentType.FromMimeType(command.ContentType!), content, cancellationToken))
            .WithMessage("The file content does not match its declared type.");
    }

    private static bool HasExtensionOf(string fileName, ContentType contentType) =>
        string.Equals(Path.GetExtension(fileName.Trim()), contentType.Extension, StringComparison.OrdinalIgnoreCase);

    private static async Task<bool> HasValidSignatureAsync(ContentType contentType, Stream content, CancellationToken cancellationToken)
    {
        if (!content.CanSeek)
        {
            throw new InvalidOperationException("Uploaded content must be seekable.");
        }

        var buffer = new byte[contentType == ContentType.Pdf ? _pdfSignature.Length : TextSampleSize];

        content.Position = 0;
        var read = await content.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        content.Position = 0;

        var header = buffer.AsSpan(0, read);

        // NUL bytes appear in binary formats, never in UTF-8 text.
        return contentType == ContentType.Pdf
            ? header.StartsWith(_pdfSignature)
            : !header.Contains((byte)0);
    }
}

internal sealed partial class UploadDocumentCommandHandler(
    IDocumentRepository documents,
    IUnitOfWork unitOfWork,
    IFileStorage storage,
    IOptions<DocumentLimitsOptions> limits,
    TimeProvider timeProvider,
    ILogger<UploadDocumentCommandHandler> logger)
    : ICommandHandler<UploadDocumentCommand, DocumentSummary>
{
    public async Task<Result<DocumentSummary>> HandleAsync(UploadDocumentCommand command, CancellationToken cancellationToken)
    {
        // Checked before anything is stored. Two uploads at the same moment can both pass: the quota
        // bounds storage per user, it does not need to be exact.
        if (await documents.CountByOwnerAsync(command.OwnerId, cancellationToken) >= limits.Value.MaxDocumentsPerUser)
        {
            return DocumentErrors.QuotaExceeded;
        }

        var contentType = ContentType.FromMimeType(command.ContentType!);
        var document = Document.Upload(
            DocumentId.New(),
            command.OwnerId,
            new FileName(command.FileName),
            contentType,
            new FileSize(command.Length),
            timeProvider.GetUtcNow());

        await storage.UploadAsync(document.StorageKey, command.Content, contentType, cancellationToken);

        // The row and the DocumentUploaded outbox message are saved in one transaction.
        documents.Add(document);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await DeleteOrphanedContentAsync(document.StorageKey);
            throw;
        }

        return DocumentSummary.From(document);
    }

    // Compensation: without the row nothing refers to the stored file.
    private async Task DeleteOrphanedContentAsync(StorageKey key)
    {
        try
        {
            await storage.DeleteAsync(key, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // Only logged: the caller rethrows the original failure.
            LogCompensationFailed(logger, exception, key.Value);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not delete orphaned content {StorageKey} after a failed upload")]
    private static partial void LogCompensationFailed(ILogger logger, Exception exception, string storageKey);
}
