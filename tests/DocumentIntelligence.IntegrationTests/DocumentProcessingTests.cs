extern alias worker;

using System.Net;
using System.Text;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Analysis;
using DocumentIntelligence.Domain.Users;
using DocumentIntelligence.Infrastructure.Persistence;
using DocumentIntelligence.IntegrationTests.Infrastructure;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Analysis_stays_counted_after_its_document_is_deleted()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var id = await UploadAsync(client, "notes.txt", "text/plain", Encoding.UTF8.GetBytes("Meeting notes of 2026-11-11."));
        (await client.WaitUntilProcessedAsync(id)).Status.ShouldBe(DocumentStatus.Completed);

        (await client.DeleteAsync($"/api/documents/{id}", CancellationToken)).EnsureSuccessStatusCode();

        // The daily limits count this log, so deleting analyzed documents cannot reset them.
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recorded = await dbContext.Database
            .SqlQuery<int>($"""SELECT COUNT(*)::int AS "Value" FROM "AnalysisUsage" WHERE "DocumentId" = {id}""")
            .SingleAsync(CancellationToken);
        recorded.ShouldBe(1);
    }

    [Fact]
    public async Task Analysis_stays_counted_when_its_document_is_deleted_during_the_analysis()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var fileName = $"deleted-{Guid.NewGuid():N}.txt";
        var content = Encoding.UTF8.GetBytes($"{DeleteDuringAnalysisAnalyzer.Marker} {fileName}");

        var id = await UploadAsync(client, fileName, "text/plain", content);

        // The final save fails on the row version, the attempt rolls back and the retry finds no document.
        (await factory.ConsumedAsync<DocumentUploaded>(message => message.DocumentId == id)).ShouldBeTrue();
        (await client.GetAsync($"/api/documents/{id}", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The AI was called, so the analysis still counts, exactly once despite the retry.
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var recorded = await dbContext.Database
            .SqlQuery<int>($"""SELECT COUNT(*)::int AS "Value" FROM "AnalysisUsage" WHERE "DocumentId" = {id}""")
            .SingleAsync(CancellationToken);
        recorded.ShouldBe(1);
    }

    [Fact]
    public async Task Redelivered_document_is_processed_again_in_isolation_rather_than_failed()
    {
        var (client, ownerId) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var document = await StorePendingDocumentWithoutMessageAsync(ownerId);

        // As if the Worker had died while holding the message, e.g. next to another, hostile document.
        await factory.Services.GetTestHarness().Bus.Publish(
            new DocumentUploaded(document.Id.Value, ownerId.Value),
            context => context.Headers.Set(DeliveryCount.DeliveryCountHeader, 1),
            CancellationToken);

        (await factory.ConsumedAsync<ProcessDocumentInIsolation>(message => message.DocumentId == document.Id.Value)).ShouldBeTrue();

        // Processed normally: it fails only because this test stored no file for it.
        var details = await client.WaitUntilProcessedAsync(document.Id.Value);
        details.Status.ShouldBe(DocumentStatus.Failed);
        details.FailureReason.ShouldBe("The file content is missing.");
    }

    [Fact]
    public async Task Document_that_keeps_crashing_the_worker_on_its_own_is_failed()
    {
        var (client, ownerId) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var document = await StorePendingDocumentWithoutMessageAsync(ownerId);

        var endpoint = await factory.Services.GetTestHarness().Bus.GetSendEndpoint(IsolatedDocumentConsumerDefinition.QueueAddress);
        await endpoint.Send(
            new ProcessDocumentInIsolation(document.Id.Value),
            context => context.Headers.Set(DeliveryCount.DeliveryCountHeader, IsolatedDocumentConsumer.MaxDeliveries),
            CancellationToken);

        var details = await client.WaitUntilProcessedAsync(document.Id.Value);
        details.Status.ShouldBe(DocumentStatus.Failed);
        details.FailureReason.ShouldBe(IsolatedDocumentConsumer.CrashedFailureReason);
    }

    // A pending document that has no DocumentUploaded message (nor file) of its own, so a test can deliver one.
    private async Task<Document> StorePendingDocumentWithoutMessageAsync(UserId ownerId)
    {
        var document = Document.Upload(
            DocumentId.New(), ownerId, new FileName("crash.pdf"), ContentType.Pdf, new FileSize(1024), DateTimeOffset.UtcNow);
        document.ClearDomainEvents();

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Add(document);
        await dbContext.SaveChangesAsync(CancellationToken);

        return document;
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
