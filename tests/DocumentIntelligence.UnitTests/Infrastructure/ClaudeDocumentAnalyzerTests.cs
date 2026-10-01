using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Anthropic;
using Anthropic.Core;
using Anthropic.Exceptions;
using DocumentIntelligence.Application.Abstractions.Analysis;
using DocumentIntelligence.Application.Documents.Processing;
using DocumentIntelligence.Infrastructure.Analysis;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.UnitTests.Infrastructure;

/// <summary>
/// Runs the real SDK client against canned Messages API responses.
/// </summary>
public sealed class ClaudeDocumentAnalyzerTests : IDisposable
{
    private const string Model = "claude-haiku-4-5-20251001";

    private static readonly object _validInput = new
    {
        document_type = "Invoice",
        summary = "An invoice from Acme for consulting services.",
        entities = new[] { new { type = "Organization", name = "Acme" } },
        important_dates = new[] { new { date = "2026-11-11", type = "PaymentDeadline", description = "Invoice payment due date" } },
        financial_information = new[] { new { description = "Total due", amount = 1200.50m, currency = "PLN" } },
        potential_risks = new[] { new { severity = "Medium", description = "Late payment interest" } },
    };

    private readonly StubHttpMessageHandler _handler = new();
    private readonly FakeLogger<ClaudeDocumentAnalyzer> _logger = new();
    private readonly AnthropicClient _client;
    private readonly ClaudeDocumentAnalyzer _analyzer;

    public ClaudeDocumentAnalyzerTests()
    {
        _client = new AnthropicClient(new ClientOptions
        {
            ApiKey = "test-key",
            HttpClient = new HttpClient(_handler),
            MaxRetries = 0,
        });
        _analyzer = new ClaudeDocumentAnalyzer(
            _client,
            new AnalysisResultValidator(),
            Options.Create(new AnthropicOptions { ApiKey = "test-key", Model = Model }),
            _logger);
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Forced_tool_call_is_parsed_into_the_analysis()
    {
        _handler.RespondWith(HttpStatusCode.OK, ToolUseResponse(_validInput));

        var result = await _analyzer.AnalyzeAsync("INVOICE 7/2026 from Acme", CancellationToken);

        result.DocumentType.ShouldBe("Invoice");
        result.Summary.ShouldBe("An invoice from Acme for consulting services.");
        result.Entities.ShouldBe([new AnalyzedEntity("Organization", "Acme")]);
        result.ImportantDates.ShouldBe([new AnalyzedDate("2026-11-11", "PaymentDeadline", "Invoice payment due date")]);
        result.FinancialInformation.ShouldBe([new AnalyzedAmount("Total due", 1200.50m, "PLN")]);
        result.PotentialRisks.ShouldBe([new AnalyzedRisk("Medium", "Late payment interest")]);

        var request = JsonNode.Parse(_handler.Requests.ShouldHaveSingleItem().Body)!;
        request["model"]!.GetValue<string>().ShouldBe(Model);
        request["tool_choice"]!["type"]!.GetValue<string>().ShouldBe("tool");
        request["tool_choice"]!["name"]!.GetValue<string>().ShouldBe(RecordAnalysisTool.Name);
        request["tools"]![0]!["name"]!.GetValue<string>().ShouldBe(RecordAnalysisTool.Name);
        request["tools"]![0]!["input_schema"]!["required"]!.AsArray().Count.ShouldBe(6);
        request["messages"]![0]!["content"]!.GetValue<string>().ShouldContain("<document>\nINVOICE 7/2026 from Acme\n</document>");
    }

    [Fact]
    public async Task Invalid_analysis_is_sent_back_once_with_the_validation_errors()
    {
        var invalidInput = JsonSerializer.SerializeToNode(_validInput)!;
        invalidInput["important_dates"]![0]!["date"] = "11.11.2026";
        _handler
            .RespondWith(HttpStatusCode.OK, ToolUseResponse(invalidInput, toolUseId: "toolu_first"))
            .RespondWith(HttpStatusCode.OK, ToolUseResponse(_validInput));

        var result = await _analyzer.AnalyzeAsync("INVOICE", CancellationToken);

        result.ImportantDates.ShouldHaveSingleItem().Date.ShouldBe("2026-11-11");
        _handler.Requests.Count.ShouldBe(2);

        var messages = JsonNode.Parse(_handler.Requests[1].Body)!["messages"]!.AsArray();
        messages.Count.ShouldBe(3);
        messages[1]!["role"]!.GetValue<string>().ShouldBe("assistant");
        messages[1]!["content"]![0]!["id"]!.GetValue<string>().ShouldBe("toolu_first");
        var toolResult = messages[2]!["content"]![0]!;
        toolResult["type"]!.GetValue<string>().ShouldBe("tool_result");
        toolResult["tool_use_id"]!.GetValue<string>().ShouldBe("toolu_first");
        toolResult["is_error"]!.GetValue<bool>().ShouldBeTrue();
        toolResult["content"]!.GetValue<string>().ShouldContain("ImportantDates[0].Date");
        toolResult["content"]!.GetValue<string>().ShouldContain("11.11.2026");

        // The log names the problem, but not the value: model output may carry document text.
        var logged = _logger.Collector.GetSnapshot().ShouldHaveSingleItem().Message;
        logged.ShouldContain("ImportantDates[0].Date");
        logged.ShouldNotContain("11.11.2026");
    }

    [Fact]
    public async Task Analysis_still_invalid_after_the_correction_is_unprocessable()
    {
        var invalidInput = JsonSerializer.SerializeToNode(_validInput)!;
        invalidInput["summary"] = "";
        _handler
            .RespondWith(HttpStatusCode.OK, ToolUseResponse(invalidInput))
            .RespondWith(HttpStatusCode.OK, ToolUseResponse(invalidInput));

        var exception = await Should.ThrowAsync<UnprocessableDocumentException>(
            () => _analyzer.AnalyzeAsync("INVOICE", CancellationToken));

        exception.Message.ShouldBe("The AI returned an invalid analysis.");
        _handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Input_that_does_not_match_the_schema_types_is_corrected_like_invalid_values()
    {
        var invalidInput = JsonSerializer.SerializeToNode(_validInput)!;
        invalidInput["financial_information"]![0]!["amount"] = "1 200,50 zł";
        _handler
            .RespondWith(HttpStatusCode.OK, ToolUseResponse(invalidInput))
            .RespondWith(HttpStatusCode.OK, ToolUseResponse(_validInput));

        var result = await _analyzer.AnalyzeAsync("INVOICE", CancellationToken);

        result.FinancialInformation.ShouldHaveSingleItem().Amount.ShouldBe(1200.50m);
    }

    [Fact]
    public async Task Refusal_is_unprocessable()
    {
        _handler.RespondWith(HttpStatusCode.OK, MessageResponse([new { type = "text", text = "I can't help with that." }], "refusal"));

        var exception = await Should.ThrowAsync<UnprocessableDocumentException>(
            () => _analyzer.AnalyzeAsync("INVOICE", CancellationToken));

        exception.Message.ShouldBe("The AI declined to analyze the document.");
    }

    [Fact]
    public async Task Rejected_request_is_unprocessable()
    {
        _handler.RespondWith(HttpStatusCode.BadRequest, ErrorResponse("invalid_request_error", "prompt is too long"));

        await Should.ThrowAsync<UnprocessableDocumentException>(() => _analyzer.AnalyzeAsync("INVOICE", CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "rate_limit_error")]
    [InlineData(HttpStatusCode.InternalServerError, "api_error")]
    public async Task Service_errors_propagate_to_be_retried(HttpStatusCode statusCode, string errorType)
    {
        _handler.RespondWith(statusCode, ErrorResponse(errorType, "try again later"));

        var exception = await Should.ThrowAsync<AnthropicApiException>(() => _analyzer.AnalyzeAsync("INVOICE", CancellationToken));

        exception.StatusCode.ShouldBe(statusCode);
    }

    private static string ToolUseResponse(object input, string toolUseId = "toolu_01") =>
        MessageResponse([new { type = "tool_use", id = toolUseId, name = RecordAnalysisTool.Name, input }], "tool_use");

    private static string MessageResponse(object[] content, string stopReason) =>
        JsonSerializer.Serialize(new
        {
            id = "msg_01",
            type = "message",
            role = "assistant",
            model = Model,
            content,
            stop_reason = stopReason,
            stop_sequence = (string?)null,
            usage = new { input_tokens = 100, output_tokens = 50 },
        });

    private static string ErrorResponse(string type, string message) =>
        JsonSerializer.Serialize(new { type = "error", error = new { type, message } });
}
