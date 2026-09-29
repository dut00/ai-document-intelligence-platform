using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentIntelligence.Application.Documents;
using DocumentIntelligence.Domain.Documents;

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

public static class DocumentApiExtensions
{
    private static readonly TimeSpan _processingTimeout = TimeSpan.FromSeconds(30);

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> UploadDocumentAsync(this HttpClient client, string fileName, string contentType, byte[] content)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };

        return client.PostAsync("/api/documents", form, CancellationToken);
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync(CancellationToken));
        return (await response.Content.ReadFromJsonAsync<T>(_json, CancellationToken))!;
    }

    public static async Task<T> GetAsync<T>(this HttpClient client, string path) =>
        await (await client.GetAsync(path, CancellationToken)).ReadAsync<T>();

    /// <summary>
    /// Polls the details until the Worker's consumers have finished with the document.
    /// </summary>
    public static async Task<DocumentDetailsResponse> WaitUntilProcessedAsync(this HttpClient client, Guid documentId)
    {
        var started = Stopwatch.GetTimestamp();

        while (true)
        {
            var details = await client.GetAsync<DocumentDetailsResponse>($"/api/documents/{documentId}");
            if (details.Status is DocumentStatus.Completed or DocumentStatus.Failed)
            {
                return details;
            }

            Stopwatch.GetElapsedTime(started).ShouldBeLessThan(_processingTimeout, $"Document {documentId} is still {details.Status}.");
            await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        }
    }
}
