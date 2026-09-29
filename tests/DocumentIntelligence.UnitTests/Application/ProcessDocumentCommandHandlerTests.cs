using System.Text;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Application.Abstractions.Calendar;
using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Application.Documents.Processing;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Analysis;
using DocumentIntelligence.Domain.Documents.Events;
using DocumentIntelligence.Domain.Users;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace DocumentIntelligence.UnitTests.Application;

public sealed class ProcessDocumentCommandHandlerTests
{
    private const string DocumentText = "Invoice 7/2026. Payment due 2026-11-11.";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly IDocumentRepository _documents = Substitute.For<IDocumentRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFileStorage _storage = Substitute.For<IFileStorage>();
    private readonly ITextExtractor _extractor = Substitute.For<ITextExtractor>();
    private readonly IDocumentAnalyzer _analyzer = Substitute.For<IDocumentAnalyzer>();
    private readonly IPublicHolidayProvider _holidays = Substitute.For<IPublicHolidayProvider>();
    private readonly ProcessDocumentCommandHandler _handler;

    public ProcessDocumentCommandHandlerTests()
    {
        _storage.OpenReadAsync(Arg.Any<StorageKey>(), Arg.Any<CancellationToken>())
            .Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes(DocumentText)));
        _extractor.CanExtract(ContentType.Txt).Returns(true);
        _extractor.ExtractTextAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(DocumentText);
        _analyzer.Model.Returns("test-model");
        _analyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(AnalysisResult());
        _holidays.GetPublicHolidaysAsync(2026, Arg.Any<CancellationToken>())
            .Returns([new PublicHoliday(new DateOnly(2026, 11, 11), "National Independence Day")]);

        _handler = new ProcessDocumentCommandHandler(
            _documents,
            _unitOfWork,
            _storage,
            [_extractor],
            _analyzer,
            new DateInsightsService(_holidays, NullLogger<DateInsightsService>.Instance),
            _time,
            NullLogger<ProcessDocumentCommandHandler>.Instance);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Pending_document_is_analyzed_and_completed()
    {
        var document = Stored(PendingDocument());

        var outcome = await ProcessAsync(document);

        outcome.ShouldBe(ProcessingOutcome.Completed);
        document.Status.ShouldBe(DocumentStatus.Completed);
        document.ProcessedAt.ShouldBe(_time.GetUtcNow());
        document.DomainEvents.OfType<DocumentProcessingCompletedDomainEvent>().ShouldHaveSingleItem();
        await _analyzer.Received(1).AnalyzeAsync(DocumentText, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        var analysis = document.Analysis.ShouldNotBeNull();
        analysis.Model.ShouldBe("test-model");
        analysis.DocumentType.ShouldBe("Invoice");
        analysis.Entities.ShouldBe([new ExtractedEntity(EntityType.Organization, "Acme")]);
        analysis.FinancialInformation.ShouldBe([new FinancialItem("Total due", new Money(1200m, "PLN"))]);
        analysis.PotentialRisks.ShouldBe([new Risk(RiskSeverity.High, "Unlimited liability")]);
        analysis.ImportantDates.ShouldBe(
        [
            new ImportantDate(
                new DateOnly(2026, 11, 11),
                ImportantDateType.PaymentDeadline,
                "Invoice payment due date",
                new CalendarCheck(isWeekend: false, "National Independence Day", new DateOnly(2026, 11, 12))),
        ]);
    }

    [Fact]
    public async Task Unavailable_holiday_calendar_still_completes_the_document()
    {
        _holidays.GetPublicHolidaysAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Nager.Date is down"));
        var document = Stored(PendingDocument());

        var outcome = await ProcessAsync(document);

        outcome.ShouldBe(ProcessingOutcome.Completed);
        document.Analysis!.ImportantDates.ShouldHaveSingleItem().CalendarCheck.ShouldBeNull();
    }

    [Fact]
    public async Task Deleted_document_is_skipped()
    {
        var outcome = await _handler.HandleAsync(new ProcessDocumentCommand(DocumentId.New()), CancellationToken);

        outcome.Value.ShouldBe(ProcessingOutcome.Skipped);
        await _storage.DidNotReceiveWithAnyArgs().OpenReadAsync(default!, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task Completed_document_is_skipped()
    {
        var document = Stored(PendingDocument());
        await ProcessAsync(document);
        _analyzer.ClearReceivedCalls();

        var outcome = await ProcessAsync(document);

        outcome.ShouldBe(ProcessingOutcome.Skipped);
        await _analyzer.DidNotReceiveWithAnyArgs().AnalyzeAsync(default!, CancellationToken);
    }

    [Fact]
    public async Task Document_being_processed_is_skipped_until_it_goes_stale()
    {
        var document = Stored(PendingDocument());
        document.StartProcessing(_time.GetUtcNow(), ProcessDocumentCommandHandler.StaleProcessingTimeout);

        (await ProcessAsync(document)).ShouldBe(ProcessingOutcome.Skipped);

        _time.Advance(ProcessDocumentCommandHandler.StaleProcessingTimeout);

        (await ProcessAsync(document)).ShouldBe(ProcessingOutcome.Completed);
    }

    [Fact]
    public async Task Missing_content_fails_the_document()
    {
        _storage.OpenReadAsync(Arg.Any<StorageKey>(), Arg.Any<CancellationToken>()).Returns((Stream?)null);
        var document = Stored(PendingDocument());

        var outcome = await ProcessAsync(document);

        outcome.ShouldBe(ProcessingOutcome.Failed);
        document.FailureReason.ShouldBe("The file content is missing.");
    }

    [Fact]
    public async Task Document_without_text_fails_without_calling_the_analyzer()
    {
        _extractor.ExtractTextAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns("  \n ");
        var document = Stored(PendingDocument());

        var outcome = await ProcessAsync(document);

        outcome.ShouldBe(ProcessingOutcome.Failed);
        document.Status.ShouldBe(DocumentStatus.Failed);
        document.FailureReason.ShouldNotBeNull().ShouldContain("no extractable text");
        document.DomainEvents.OfType<DocumentProcessingFailedDomainEvent>().ShouldHaveSingleItem();
        await _analyzer.DidNotReceiveWithAnyArgs().AnalyzeAsync(default!, CancellationToken);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Permanent_analysis_failure_fails_the_document()
    {
        _analyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnprocessableDocumentException("The AI returned an invalid analysis."));
        var document = Stored(PendingDocument());

        var outcome = await ProcessAsync(document);

        outcome.ShouldBe(ProcessingOutcome.Failed);
        document.FailureReason.ShouldBe("The AI returned an invalid analysis.");
    }

    [Fact]
    public async Task Transient_failure_propagates_without_saving()
    {
        _analyzer.AnalyzeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("529 Overloaded"));
        var document = Stored(PendingDocument());

        await Should.ThrowAsync<HttpRequestException>(() => ProcessAsync(document));

        document.Status.ShouldNotBe(DocumentStatus.Failed);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task Long_text_is_truncated_before_analysis()
    {
        _extractor.ExtractTextAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(new string('a', ProcessDocumentCommandHandler.MaxAnalyzedCharacters + 100));

        await ProcessAsync(Stored(PendingDocument()));

        await _analyzer.Received(1).AnalyzeAsync(
            Arg.Is<string>(text => text.Length == ProcessDocumentCommandHandler.MaxAnalyzedCharacters),
            Arg.Any<CancellationToken>());
    }

    private async Task<ProcessingOutcome> ProcessAsync(Document document)
    {
        var result = await _handler.HandleAsync(new ProcessDocumentCommand(document.Id), CancellationToken);

        return result.Value;
    }

    private Document PendingDocument() => Document.Upload(
        DocumentId.New(), UserId.New(), new FileName("invoice.txt"), ContentType.Txt, new FileSize(100), _time.GetUtcNow());

    private Document Stored(Document document)
    {
        _documents.GetByIdAsync(document.Id, Arg.Any<CancellationToken>()).Returns(document);
        return document;
    }

    private static AnalysisResult AnalysisResult() => new(
        "Invoice",
        "An invoice from Acme.",
        [new AnalyzedEntity("Organization", "Acme")],
        [new AnalyzedDate("2026-11-11", "PaymentDeadline", "Invoice payment due date")],
        [new AnalyzedAmount("Total due", 1200m, "PLN")],
        [new AnalyzedRisk("High", "Unlimited liability")]);
}
