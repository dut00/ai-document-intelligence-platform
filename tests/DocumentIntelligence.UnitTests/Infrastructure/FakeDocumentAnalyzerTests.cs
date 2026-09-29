using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Application.Documents.Processing;
using DocumentIntelligence.Infrastructure.Analysis;
using Microsoft.Extensions.Logging.Abstractions;

namespace DocumentIntelligence.UnitTests.Infrastructure;

public sealed class FakeDocumentAnalyzerTests
{
    private readonly FakeDocumentAnalyzer _analyzer = new(NullLogger<FakeDocumentAnalyzer>.Instance);

    [Fact]
    public async Task Finds_dates_and_amounts_deterministically()
    {
        var result = await AnalyzeAsync(
            "Umowa zlecenia z dnia 14.11.2026. Wynagrodzenie 1500,00 PLN płatne do 2026-12-01 (nie 2026-02-30). Kara 200 EUR.");

        result.DocumentType.ShouldBe("Contract");
        result.ImportantDates.Select(date => date.Date).ShouldBe(["2026-11-14", "2026-12-01"]);
        result.FinancialInformation.Select(item => (item.Amount, item.Currency)).ShouldBe([(1500.00m, "PLN"), (200m, "EUR")]);
        _analyzer.Model.ShouldBe(FakeDocumentAnalyzer.ModelName);
    }

    [Theory]
    [InlineData("INVOICE no. 12", "Invoice")]
    [InlineData("Faktura VAT 3/2026", "Invoice")]
    [InlineData("Dear Sir or Madam", "Document")]
    public async Task Detects_a_few_document_types(string text, string expectedType)
    {
        (await AnalyzeAsync(text)).DocumentType.ShouldBe(expectedType);
    }

    [Fact]
    public async Task Result_passes_the_analysis_validator()
    {
        var result = await AnalyzeAsync("Contract signed 2026-11-11, total 10.50 USD.");

        new AnalysisResultValidator().Validate(result).IsValid.ShouldBeTrue();
    }

    private Task<AnalysisResult> AnalyzeAsync(string text) =>
        _analyzer.AnalyzeAsync(text, TestContext.Current.CancellationToken);
}
