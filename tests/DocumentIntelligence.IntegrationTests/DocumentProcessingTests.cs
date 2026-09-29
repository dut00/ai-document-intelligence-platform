extern alias worker;

using System.Text;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Analysis;
using DocumentIntelligence.IntegrationTests.Infrastructure;
using MassTransit;
using MassTransit.Testing;
using worker::DocumentIntelligence.Worker.Consumers;

namespace DocumentIntelligence.IntegrationTests;

/// <summary>
/// Upload → DocumentUploaded (outbox) → Worker consumer → text extraction → fake analyzer → calendar check → Completed.
/// </summary>
public sealed class DocumentProcessingTests(ApiFactory factory)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Text_document_is_analyzed_and_its_dates_are_checked_against_the_calendar()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var content = Encoding.UTF8.GetBytes(
            "Service agreement signed on 2026-11-11.\nThe fee of 1500.00 PLN is due by 14.11.2026.\n");

        var id = await UploadAsync(client, "agreement.txt", "text/plain", content);
        var details = await client.WaitUntilProcessedAsync(id);

        details.Status.ShouldBe(DocumentStatus.Completed);
        details.FailureReason.ShouldBeNull();
        details.ProcessedAt.ShouldNotBeNull();
        var analysis = details.Analysis.ShouldNotBeNull();
        analysis.Model.ShouldBe("fake");
        analysis.DocumentType.ShouldBe("Contract");
        analysis.FinancialInformation.ShouldBe([new FinancialItemResponse("Amount mentioned in the document", 1500.00m, "PLN")]);
        analysis.ImportantDates.Select(date => (date.Date, date.CalendarCheck)).ShouldBe(
        [
            // A Wednesday, but a public holiday.
            (new DateOnly(2026, 11, 11), new CalendarCheckResponse(
                IsWeekend: false, IsPublicHoliday: true, StubPublicHolidayProvider.IndependenceDay, IsBusinessDay: false, new DateOnly(2026, 11, 12))),
            // A Saturday: the next business day is Monday.
            (new DateOnly(2026, 11, 14), new CalendarCheckResponse(
                IsWeekend: true, IsPublicHoliday: false, HolidayName: null, IsBusinessDay: false, new DateOnly(2026, 11, 16))),
        ]);
        analysis.ImportantDates.ShouldAllBe(date => date.Type == ImportantDateType.Other);

        (await factory.ConsumedAsync<DocumentStatusChanged>(message => message.DocumentId == id && message.Status == "Completed"))
            .ShouldBeTrue();
    }

    [Fact]
    public async Task Pdf_text_layer_is_extracted_and_analyzed()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var pdf = TestPdf.WithText("INVOICE 7/2026", "Due date: 2026-12-23", "Total: 99.90 EUR");

        var id = await UploadAsync(client, "invoice.pdf", "application/pdf", pdf);
        var details = await client.WaitUntilProcessedAsync(id);

        details.Status.ShouldBe(DocumentStatus.Completed);
        var analysis = details.Analysis.ShouldNotBeNull();
        analysis.DocumentType.ShouldBe("Invoice");
        analysis.ImportantDates.ShouldHaveSingleItem().Date.ShouldBe(new DateOnly(2026, 12, 23));
        analysis.FinancialInformation.ShouldHaveSingleItem().Amount.ShouldBe(99.90m);
    }

    [Fact]
    public async Task Pdf_without_text_layer_fails_without_retries()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);

        var id = await UploadAsync(client, "scan.pdf", "application/pdf", TestPdf.WithoutText());
        var details = await client.WaitUntilProcessedAsync(id);

        details.Status.ShouldBe(DocumentStatus.Failed);
        details.FailureReason.ShouldNotBeNull().ShouldContain("no extractable text");
        details.Analysis.ShouldBeNull();

        (await factory.ConsumedAsync<DocumentStatusChanged>(message => message.DocumentId == id && message.Status == "Failed"))
            .ShouldBeTrue();
        // A permanent failure is not retried, so the message never faults.
        (await factory.Services.GetTestHarness().Published.Any<Fault<DocumentUploaded>>(
            context => context.Context.Message.Message.DocumentId == id, CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Document_fails_once_transient_errors_exhaust_the_retries()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var text = $"Contract {TransientFailureAnalyzer.Marker} {Guid.NewGuid()}";

        var id = await UploadAsync(client, "flaky.txt", "text/plain", Encoding.UTF8.GetBytes(text));
        var details = await client.WaitUntilProcessedAsync(id);

        details.Status.ShouldBe(DocumentStatus.Failed);
        details.FailureReason.ShouldBe(DocumentUploadedFaultConsumer.FailureReason);
        TransientFailureAnalyzer.AttemptsFor(text).ShouldBe(ApiFactory.MessageRetryLimit + 1);
        (await factory.PublishedAsync<Fault<DocumentUploaded>>(fault => fault.Message.DocumentId == id)).ShouldBeTrue();
        (await factory.ConsumedAsync<DocumentStatusChanged>(message => message.DocumentId == id && message.Status == "Failed"))
            .ShouldBeTrue();
    }

    private static async Task<Guid> UploadAsync(HttpClient client, string fileName, string contentType, byte[] content) =>
        (await (await client.UploadDocumentAsync(fileName, contentType, content)).ReadAsync<DocumentSummary>()).Id;
}
