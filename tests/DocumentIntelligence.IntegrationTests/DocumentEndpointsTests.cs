using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentIntelligence.Application.Abstractions.Paging;
using DocumentIntelligence.Application.Abstractions.Storage;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Contracts.Documents;
using DocumentIntelligence.Domain.Abstractions;
using DocumentIntelligence.Domain.Documents;
using DocumentIntelligence.Domain.Documents.Analysis;
using DocumentIntelligence.Domain.Users;
using DocumentIntelligence.IntegrationTests.Infrastructure;
using MassTransit.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DocumentIntelligence.IntegrationTests;

public sealed class DocumentEndpointsTests(ApiFactory factory)
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly byte[] _textContent = "Payment is due on 11 November 2026.\nTotal: 1200 PLN\n"u8.ToArray();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Upload_list_details_download_delete_flow()
    {
        var (client, userId) = await factory.CreateAuthenticatedClientAsync(CancellationToken);

        var upload = await UploadAsync(client, "invoice.txt", "text/plain", _textContent);
        upload.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var uploaded = await ReadAsync<DocumentSummary>(upload);
        uploaded.Status.ShouldBe(DocumentStatus.Pending);
        upload.Headers.Location!.ToString().ShouldBe($"/api/documents/{uploaded.Id}");
        (await PublishedAsync<DocumentUploaded>(message => message.DocumentId == uploaded.Id)).ShouldBeTrue();

        var list = await ReadAsync<PagedResponse<DocumentSummary>>(await client.GetAsync("/api/documents", CancellationToken));
        list.Items.ShouldHaveSingleItem().Id.ShouldBe(uploaded.Id);

        var details = await ReadAsync<DocumentDetailsResponse>(await client.GetAsync($"/api/documents/{uploaded.Id}", CancellationToken));
        details.FileName.ShouldBe("invoice.txt");
        details.ContentType.ShouldBe("text/plain");
        details.SizeBytes.ShouldBe(_textContent.Length);
        details.Analysis.ShouldBeNull();

        var download = await client.GetAsync($"/api/documents/{uploaded.Id}/download", CancellationToken);
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        download.Content.Headers.ContentDisposition!.FileName.ShouldBe("invoice.txt");
        (await download.Content.ReadAsByteArrayAsync(CancellationToken)).ShouldBe(_textContent);

        var delete = await client.DeleteAsync($"/api/documents/{uploaded.Id}", CancellationToken);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/documents/{uploaded.Id}", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/documents/{uploaded.Id}/download", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        // The Worker's consumer removes the stored content once DocumentDeleted leaves the outbox.
        (await ConsumedAsync<DocumentDeleted>(message => message.DocumentId == uploaded.Id)).ShouldBeTrue();
        await using var scope = factory.Services.CreateAsyncScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var storageKey = StorageKey.For(userId, new DocumentId(uploaded.Id));
        (await storage.OpenReadAsync(storageKey, CancellationToken)).ShouldBeNull();
    }

    [Fact]
    public async Task List_is_paginated_newest_first_and_filtered_by_status()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var ids = new List<Guid>();
        foreach (var name in new[] { "a.txt", "b.txt", "c.txt" })
        {
            ids.Add((await ReadAsync<DocumentSummary>(await UploadAsync(client, name, "text/plain", _textContent))).Id);
        }

        var firstPage = await ReadAsync<PagedResponse<DocumentSummary>>(
            await client.GetAsync("/api/documents?page=1&pageSize=2", CancellationToken));
        firstPage.TotalCount.ShouldBe(3);
        firstPage.TotalPages.ShouldBe(2);
        firstPage.Items.Select(item => item.Id).ShouldBe([ids[2], ids[1]]);

        var secondPage = await ReadAsync<PagedResponse<DocumentSummary>>(
            await client.GetAsync("/api/documents?page=2&pageSize=2", CancellationToken));
        secondPage.Items.ShouldHaveSingleItem().Id.ShouldBe(ids[0]);

        var completed = await ReadAsync<PagedResponse<DocumentSummary>>(
            await client.GetAsync("/api/documents?status=Completed", CancellationToken));
        completed.TotalCount.ShouldBe(0);

        var stats = await ReadAsync<DocumentStatsResponse>(await client.GetAsync("/api/documents/stats", CancellationToken));
        stats.ShouldBe(new DocumentStatsResponse(Total: 3, Pending: 3, Processing: 0, Completed: 0, Failed: 0));
    }

    [Fact]
    public async Task Invalid_page_size_returns_validation_problem()
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);

        var response = await client.GetAsync("/api/documents?pageSize=1000", CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(CancellationToken))!.Errors.ShouldContainKey("PageSize");
    }

    [Theory]
    [InlineData("scan.pdf", "application/pdf", "not a pdf")]
    [InlineData("notes.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "PK")]
    [InlineData("notes.pdf", "text/plain", "plain text")]
    public async Task Invalid_file_is_rejected_with_validation_problem(string fileName, string contentType, string content)
    {
        var (client, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);

        var response = await UploadAsync(client, fileName, contentType, Encoding.UTF8.GetBytes(content));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(CancellationToken))!.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Another_users_document_is_not_found()
    {
        var (owner, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var (stranger, _) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var document = await ReadAsync<DocumentSummary>(await UploadAsync(owner, "private.txt", "text/plain", _textContent));

        (await stranger.GetAsync($"/api/documents/{document.Id}", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.GetAsync($"/api/documents/{document.Id}/download", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await stranger.DeleteAsync($"/api/documents/{document.Id}", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ReadAsync<PagedResponse<DocumentSummary>>(await stranger.GetAsync("/api/documents", CancellationToken))).TotalCount.ShouldBe(0);

        (await owner.GetAsync($"/api/documents/{document.Id}", CancellationToken)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("/api/documents")]
    [InlineData("/api/documents/stats")]
    [InlineData("/api/documents/00000000-0000-0000-0000-000000000001")]
    public async Task Documents_endpoints_require_authentication(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path, CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Completed_document_details_include_the_stored_analysis()
    {
        var (client, userId) = await factory.CreateAuthenticatedClientAsync(CancellationToken);
        var documentId = await SeedCompletedDocumentAsync(userId);

        var details = await ReadAsync<DocumentDetailsResponse>(await client.GetAsync($"/api/documents/{documentId}", CancellationToken));

        details.Status.ShouldBe(DocumentStatus.Completed);
        details.ProcessedAt.ShouldNotBeNull();
        var analysis = details.Analysis.ShouldNotBeNull();
        analysis.DocumentType.ShouldBe("Invoice");
        analysis.Entities.ShouldBe([new EntityResponse(EntityType.Organization, "Acme")]);
        analysis.FinancialInformation.ShouldBe([new FinancialItemResponse("Total due", 1200m, "PLN")]);
        analysis.PotentialRisks.ShouldBe([new RiskResponse(RiskSeverity.Medium, "Late payment penalty")]);
        analysis.ImportantDates.Count.ShouldBe(2);
        analysis.ImportantDates[0].CalendarCheck.ShouldBe(new CalendarCheckResponse(
            IsWeekend: false,
            IsPublicHoliday: true,
            HolidayName: "National Independence Day",
            IsBusinessDay: false,
            NextBusinessDay: new DateOnly(2026, 11, 12)));
        analysis.ImportantDates[1].CalendarCheck.ShouldBeNull();

        (await PublishedAsync<DocumentStatusChanged>(message => message.DocumentId == documentId && message.Status == "Completed"))
            .ShouldBeTrue();
    }

    private async Task<Guid> SeedCompletedDocumentAsync(UserId ownerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var documents = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var now = DateTimeOffset.UtcNow;

        var document = Document.Upload(
            DocumentId.New(), ownerId, new FileName("invoice.pdf"), ContentType.Pdf, new FileSize(1024), now);
        document.StartProcessing(now, TimeSpan.FromMinutes(10));
        document.Complete(
            DocumentAnalysis.Create(
                document.Id,
                "Invoice",
                "An invoice from Acme.",
                [new ExtractedEntity(EntityType.Organization, "Acme")],
                [
                    new ImportantDate(
                        new DateOnly(2026, 11, 11),
                        ImportantDateType.PaymentDeadline,
                        "Invoice payment due date",
                        new CalendarCheck(isWeekend: false, "National Independence Day", new DateOnly(2026, 11, 12))),
                    new ImportantDate(new DateOnly(2026, 10, 1), ImportantDateType.SigningDate, "Signed"),
                ],
                [new FinancialItem("Total due", new Money(1200m, "PLN"))],
                [new Risk(RiskSeverity.Medium, "Late payment penalty")],
                "fake",
                now),
            now);

        documents.Add(document);
        await unitOfWork.SaveChangesAsync(CancellationToken);

        return document.Id.Value;
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, string contentType, byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };

        return client.PostAsync("/api/documents", form, CancellationToken);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync(CancellationToken));
        return (await response.Content.ReadFromJsonAsync<T>(_json, CancellationToken))!;
    }

    private Task<bool> PublishedAsync<T>(Func<T, bool> filter)
        where T : class =>
        factory.Services.GetTestHarness().Published.Any<T>(context => filter(context.Context.Message), CancellationToken);

    private Task<bool> ConsumedAsync<T>(Func<T, bool> filter)
        where T : class =>
        factory.Services.GetTestHarness().Consumed.Any<T>(context => filter(context.Context.Message), CancellationToken);
}
