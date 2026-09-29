using System.Net;
using System.Text;

namespace DocumentIntelligence.UnitTests.Infrastructure;

/// <summary>
/// Answers HTTP requests with queued responses and records each request with its body.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    public StubHttpMessageHandler RespondWith(HttpStatusCode statusCode, string json)
    {
        _responses.Enqueue(() => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        Requests.Add((request, body));

        return _responses.Count > 0
            ? _responses.Dequeue()()
            : throw new InvalidOperationException($"No response queued for {request.Method} {request.RequestUri}.");
    }
}
