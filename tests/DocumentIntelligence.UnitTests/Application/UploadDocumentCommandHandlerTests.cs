using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Events;
using DocumentIntelligence.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DocumentIntelligence.UnitTests.Application;

public sealed class UploadDocumentCommandHandlerTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly UploadDocumentCommandHandler _handler;

    private readonly DocumentLimitsOptions _limits = new() { MaxDocumentsPerUser = 3 };

    public UploadDocumentCommandHandlerTests() =>
        _handler = new UploadDocumentCommandHandler(
            _documents, _unitOfWork, _storage, Options.Create(_limits), new FakeTimeProvider(_now), NullLogger<UploadDocumentCommandHandler>.Instance);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stores_content_and_saves_pending_document()
    {
        var ownerId = UserId.New();
        var command = Command(ownerId);

        var result = await _handler.HandleAsync(command, CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe(DocumentStatus.Pending);
        result.Value.UploadedAt.ShouldBe(_now);

        var document = (Document)_documents.ReceivedCalls()
            .Single(call => call.GetMethodInfo().Name == nameof(IDocumentRepository.Add)).GetArguments()[0]!;
        document.OwnerId.ShouldBe(ownerId);
        document.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<DocumentUploadedDomainEvent>();
        await _storage.Received(1).UploadAsync(document.StorageKey, command.Content, ContentType.Txt, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Owner_at_the_document_quota_is_refused_before_anything_is_stored()
    {
        var ownerId = UserId.New();
        _documents.CountByOwnerAsync(ownerId, Arg.Any<CancellationToken>()).Returns(_limits.MaxDocumentsPerUser);

        var result = await _handler.HandleAsync(Command(ownerId), CancellationToken);

        result.Error.ShouldBe(DocumentErrors.QuotaExceeded);
        await _storage.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default!, CancellationToken);
        _documents.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task Failed_save_deletes_the_stored_content_and_rethrows()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database down"));

        await Should.ThrowAsync<InvalidOperationException>(() => _handler.HandleAsync(Command(UserId.New()), CancellationToken));

        var storedKey = (StorageKey)_storage.ReceivedCalls().First().GetArguments()[0]!;
        await _storage.Received(1).DeleteAsync(storedKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Failed_compensation_does_not_hide_the_original_error()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database down"));
        _storage.DeleteAsync(Arg.Any<StorageKey>(), Arg.Any<CancellationToken>()).ThrowsAsync(new IOException("storage down"));

        var exception = await Should.ThrowAsync<InvalidOperationException>(
            () => _handler.HandleAsync(Command(UserId.New()), CancellationToken));

        exception.Message.ShouldBe("database down");
    }

    private static UploadDocumentCommand Command(UserId ownerId) =>
        new(ownerId, "notes.txt", "text/plain", 5, new MemoryStream("hello"u8.ToArray()));
}
