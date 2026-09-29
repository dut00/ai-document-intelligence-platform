using System.Globalization;
using System.Text.RegularExpressions;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Domain.Documents.Analysis;
using Microsoft.Extensions.Logging;

namespace DocumentIntelligence.Infrastructure.Analysis;

/// <summary>
/// A deterministic stand-in for Claude, used when no API key is configured and in tests.
/// It finds dates and amounts with regular expressions so the rest of the pipeline
/// (calendar checks, storage, UI) still has something to work with.
/// </summary>
internal sealed partial class FakeDocumentAnalyzer : IDocumentAnalyzer
{
    public const string ModelName = "fake";

    private const int MaxItems = 20;

    public FakeDocumentAnalyzer(ILogger<FakeDocumentAnalyzer> logger) =>
        LogFakeAnalyzerInUse(logger, AnthropicOptions.ApiKeyVariable);

    public string Model => ModelName;

    public Task<AnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

        var result = new AnalysisResult(
            DetectDocumentType(text),
            $"Placeholder analysis of a {words}-word document, produced without AI. " +
            $"Set {AnthropicOptions.ApiKeyVariable} to analyze documents with Claude.",
            Entities: [],
            FindDates(text),
            FindAmounts(text),
            PotentialRisks: []);

        return Task.FromResult(result);
    }

    private static string DetectDocumentType(string text) =>
        text.Contains("invoice", StringComparison.OrdinalIgnoreCase) || text.Contains("faktura", StringComparison.OrdinalIgnoreCase)
            ? "Invoice"
            : text.Contains("agreement", StringComparison.OrdinalIgnoreCase) || text.Contains("contract", StringComparison.OrdinalIgnoreCase)
                || text.Contains("umowa", StringComparison.OrdinalIgnoreCase)
                ? "Contract"
                : "Document";

    private static List<AnalyzedDate> FindDates(string text) =>
        IsoDateRegex().Matches(text)
            .Concat(DottedDateRegex().Matches(text))
            .Select(match => TryCreateDate(match, out var date) ? date : (DateOnly?)null)
            .OfType<DateOnly>()
            .Distinct()
            .Order()
            .Take(MaxItems)
            .Select(date => new AnalyzedDate(
                date.ToString(AnalysisResult.DateFormat, CultureInfo.InvariantCulture),
                nameof(ImportantDateType.Other),
                "Date mentioned in the document"))
            .ToList();

    private static bool TryCreateDate(Match match, out DateOnly date)
    {
        var year = int.Parse(match.Groups["year"].ValueSpan, CultureInfo.InvariantCulture);
        var month = int.Parse(match.Groups["month"].ValueSpan, CultureInfo.InvariantCulture);
        var day = int.Parse(match.Groups["day"].ValueSpan, CultureInfo.InvariantCulture);

        var valid = month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);
        date = valid ? new DateOnly(year, month, day) : default;

        return valid;
    }

    private static List<AnalyzedAmount> FindAmounts(string text) =>
        AmountRegex().Matches(text)
            .Take(MaxItems)
            .Select(match => new AnalyzedAmount(
                "Amount mentioned in the document",
                decimal.Parse(match.Groups["amount"].Value.Replace(',', '.'), CultureInfo.InvariantCulture),
                match.Groups["currency"].Value))
            .ToList();

    [GeneratedRegex(@"\b(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2})\b")]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(@"\b(?<day>\d{1,2})\.(?<month>\d{1,2})\.(?<year>\d{4})\b")]
    private static partial Regex DottedDateRegex();

    [GeneratedRegex(@"\b(?<amount>\d+(?:[.,]\d{1,2})?)\s?(?<currency>PLN|EUR|USD|GBP|CHF)\b")]
    private static partial Regex AmountRegex();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Documents are analyzed by the fake analyzer: set {ApiKeyVariable} to use Claude")]
    private static partial void LogFakeAnalyzerInUse(ILogger logger, string apiKeyVariable);
}
