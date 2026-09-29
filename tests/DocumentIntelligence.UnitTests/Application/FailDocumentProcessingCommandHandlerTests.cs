using DocumentIntelligence.Application.Documents.Processing;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Users;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;

namespace DocumentIntelligence.UnitTests.Application;

public sealed class FailDocumentProcessingCommandHandlerTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly MetricCollector<long> _processed;
    private readonly FailDocumentProcessingCommandHandler _handler;

    public FailDocumentProcessingCommandHandlerTests()
    {
        _processed = new MetricCollector<long>(_meterFactory, DocumentProcessingMetrics.MeterName, "documents.processed");
        _handler = new FailDocumentProcessingCommandHandler(_documents, _unitOfWork, _time, new DocumentProcessingMetrics(_meterFactory));
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _processed.Dispose();
        _meterFactory.Dispose();
    }

    [Fact]
    public async Task Pending_document_is_failed_with_the_reason()
    {
        var document = Stored(PendingDocument());

        var result = await _handler.HandleAsync(new FailDocumentProcessingCommand(document.Id, "Gave up"), CancellationToken);

        result.Value.ShouldBe(ProcessingOutcome.Failed);
        document.Status.ShouldBe(DocumentStatus.Failed);
        document.FailureReason.ShouldBe("Gave up");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        _processed.GetMeasurementSnapshot().ShouldHaveSingleItem().Tags["outcome"].ShouldBe("failed");
    }

    [Fact]
    public async Task Finished_document_is_left_alone()
    {
        var document = Stored(PendingDocument());
        document.Fail("First failure", _time.GetUtcNow());

        var result = await _handler.HandleAsync(new FailDocumentProcessingCommand(document.Id, "Gave up"), CancellationToken);

        result.Value.ShouldBe(ProcessingOutcome.Skipped);
        document.FailureReason.ShouldBe("First failure");
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
        _processed.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task Deleted_document_is_skipped()
    {
        var result = await _handler.HandleAsync(new FailDocumentProcessingCommand(DocumentId.New(), "Gave up"), CancellationToken);

        result.Value.ShouldBe(ProcessingOutcome.Skipped);
    }

    private Document PendingDocument() => Document.Upload(
        DocumentId.New(), UserId.New(), new FileName("notes.txt"), ContentType.Txt, new FileSize(10), _time.GetUtcNow());

    private Document Stored(Document document)
    {
        _documents.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        return document;
    }
}
