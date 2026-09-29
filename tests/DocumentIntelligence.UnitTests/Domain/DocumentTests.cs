using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Analysis;
using DocumentIntelligence.Domain.Documents.Events;
using DocumentIntelligence.Domain.Users;

namespace DocumentIntelligence.UnitTests.Domain;

public sealed class DocumentTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan _staleAfter = TimeSpan.FromMinutes(10);

    [Fact]
    public void Upload_creates_pending_document_and_raises_uploaded_event()
    {
        var ownerId = UserId.New();

        var document = Upload(ownerId);

        document.Status.ShouldBe(DocumentStatus.Pending);
        document.StorageKey.Value.ShouldBe($"users/{ownerId}/{document.Id}");
        document.UploadedAt.ShouldBe(_now);
        document.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new DocumentUploadedDomainEvent(document.Id, ownerId));
    }

    [Fact]
    public void Upload_rejects_extension_that_does_not_match_content_type()
    {
        Should.Throw<DomainException>(() => Document.Upload(
            DocumentId.New(), UserId.New(), new FileName("notes.txt"), ContentType.Pdf, new FileSize(10), _now));
    }

    [Fact]
    public void Full_lifecycle_pending_processing_completed()
    {
        var document = Upload();

        document.StartProcessing(_now.AddSeconds(1), _staleAfter);
        document.Complete(CreateAnalysis(document.Id), _now.AddSeconds(5));

        document.Status.ShouldBe(DocumentStatus.Completed);
        document.Analysis.ShouldNotBeNull();
        document.ProcessedAt.ShouldBe(_now.AddSeconds(5));
        document.DomainEvents.OfType<DocumentProcessingCompletedDomainEvent>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Processing_document_cannot_be_started_again_until_it_is_stale()
    {
        var document = Upload();
        document.StartProcessing(_now, _staleAfter);

        document.CanStartProcessing(_now.AddMinutes(1), _staleAfter).ShouldBeFalse();
        Should.Throw<DomainException>(() => document.StartProcessing(_now.AddMinutes(1), _staleAfter));

        document.CanStartProcessing(_now.Add(_staleAfter), _staleAfter).ShouldBeTrue();
        document.StartProcessing(_now.Add(_staleAfter), _staleAfter);
        document.UpdatedAt.ShouldBe(_now.Add(_staleAfter));
    }

    [Theory]
    [InlineData(DocumentStatus.Pending)]
    [InlineData(DocumentStatus.Completed)]
    [InlineData(DocumentStatus.Failed)]
    public void Complete_requires_processing_status(DocumentStatus status)
    {
        var document = UploadInStatus(status);

        Should.Throw<DomainException>(() => document.Complete(CreateAnalysis(document.Id), _now));
    }

    [Fact]
    public void Complete_rejects_analysis_of_another_document()
    {
        var document = UploadInStatus(DocumentStatus.Processing);

        Should.Throw<DomainException>(() => document.Complete(CreateAnalysis(DocumentId.New()), _now));
    }

    [Theory]
    [InlineData(DocumentStatus.Pending)]
    [InlineData(DocumentStatus.Processing)]
    public void Fail_from_pending_or_processing_records_reason_and_raises_event(DocumentStatus status)
    {
        var document = UploadInStatus(status);

        document.Fail("  no extractable text  ", _now);

        document.Status.ShouldBe(DocumentStatus.Failed);
        document.FailureReason.ShouldBe("no extractable text");
        document.DomainEvents.OfType<DocumentProcessingFailedDomainEvent>().ShouldHaveSingleItem()
            .Reason.ShouldBe("no extractable text");
    }

    [Theory]
    [InlineData(DocumentStatus.Completed)]
    [InlineData(DocumentStatus.Failed)]
    public void Fail_is_not_allowed_after_processing_finished(DocumentStatus status)
    {
        var document = UploadInStatus(status);

        Should.Throw<DomainException>(() => document.Fail("late failure", _now));
    }

    [Fact]
    public void Fail_truncates_long_reason()
    {
        var document = Upload();

        document.Fail(new string('x', Document.MaxFailureReasonLength + 50), _now);

        document.FailureReason!.Length.ShouldBe(Document.MaxFailureReasonLength);
    }

    [Fact]
    public void MarkForDeletion_raises_event_once()
    {
        var document = UploadInStatus(DocumentStatus.Processing);

        document.MarkForDeletion();

        document.DomainEvents.OfType<DocumentDeletedDomainEvent>().ShouldHaveSingleItem()
            .StorageKey.ShouldBe(document.StorageKey);
        Should.Throw<DomainException>(document.MarkForDeletion);
    }

    private static Document Upload(UserId? ownerId = null) => Document.Upload(
        DocumentId.New(),
        ownerId ?? UserId.New(),
        new FileName("contract.pdf"),
        ContentType.Pdf,
        new FileSize(1024),
        _now);

    private static Document UploadInStatus(DocumentStatus status)
    {
        var document = Upload();

        if (status is DocumentStatus.Processing or DocumentStatus.Completed)
        {
            document.StartProcessing(_now, _staleAfter);
        }

        if (status == DocumentStatus.Completed)
        {
            document.Complete(CreateAnalysis(document.Id), _now);
        }

        if (status == DocumentStatus.Failed)
        {
            document.Fail("failed", _now);
        }

        document.ClearDomainEvents();
        return document;
    }

    private static DocumentAnalysis CreateAnalysis(DocumentId documentId) => DocumentAnalysis.Create(
        documentId,
        "Contract",
        "A service agreement.",
        [new ExtractedEntity(EntityType.Organization, "Acme")],
        [new ImportantDate(new DateOnly(2026, 11, 11), ImportantDateType.PaymentDeadline, "Invoice due")],
        [new FinancialItem("Total", new Money(1200m, "pln"))],
        [new Risk(RiskSeverity.Medium, "Automatic renewal")],
        "fake",
        _now);
}
