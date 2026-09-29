using System.Collections.Concurrent;
using DocumentIntelligence.Application.Abstractions.Analysis;

namespace DocumentIntelligence.IntegrationTests.Infrastructure;

/// <summary>
/// Decorates the configured analyzer (the fake one in tests): a document containing
/// <see cref="Marker"/> fails with a transient error on every attempt.
/// </summary>
public sealed class TransientFailureAnalyzer(IDocumentAnalyzer inner) : IDocumentAnalyzer
{
    public const string Marker = "[simulate-transient-failure]";

    private static readonly ConcurrentDictionary<string, int> _attempts = new();

    public string Model => inner.Model;

    public static int AttemptsFor(string documentText) => _attempts.GetValueOrDefault(documentText.Trim());

    public Task<AnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken)
    {
        if (!text.Contains(Marker, StringComparison.Ordinal))
        {
            return inner.AnalyzeAsync(text, cancellationToken);
        }

        _attempts.AddOrUpdate(text, 1, (_, attempts) => attempts + 1);
        throw new HttpRequestException("Simulated transient failure of the AI service.");
    }
}
