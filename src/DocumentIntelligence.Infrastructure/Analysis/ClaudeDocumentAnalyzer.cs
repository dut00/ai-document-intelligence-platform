using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using DocumentIntelligence.Application.Abstractions.Analysis;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentIntelligence.Infrastructure.Analysis;

/// <summary>
/// Analyzes a document with Claude through the forced <c>record_analysis</c> tool. The tool input is
/// validated; an invalid one is sent back once as an error tool result so the model can correct it.
/// </summary>
internal sealed partial class ClaudeDocumentAnalyzer(
    IAnthropicClient client,
    IValidator<AnalysisResult> validator,
    IOptions<AnthropicOptions> options,
    ILogger<ClaudeDocumentAnalyzer> logger)
    : IDocumentAnalyzer
{
    // The first answer plus one correction.
    private const int MaxAttempts = 2;

    private static readonly JsonSerializerOptions _toolInputOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public string Model => options.Value.Model;

    public async Task<AnalysisResult> AnalyzeAsync(string text, CancellationToken cancellationToken)
    {
        List<MessageParam> messages = [new() { Role = Role.User, Content = RecordAnalysisTool.CreatePrompt(text) }];

        for (var attempt = 1; ; attempt++)
        {
            var response = await CreateMessageAsync(messages, cancellationToken);
            var toolUse = FindToolUse(response);

            var errors = Parse(toolUse.Input, out var result);
            if (errors.Count == 0)
            {
                return result!;
            }

            LogInvalidAnalysis(logger, attempt, string.Join("; ", errors));

            if (attempt == MaxAttempts)
            {
                throw new UnprocessableDocumentException("The AI returned an invalid analysis.");
            }

            messages.Add(new()
            {
                Role = Role.Assistant,
                Content = new List<ContentBlockParam>
                {
                    new ToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input },
                },
            });
            messages.Add(new()
            {
                Role = Role.User,
                Content = new List<ContentBlockParam>
                {
                    new ToolResultBlockParam(toolUse.ID) { IsError = true, Content = RecordAnalysisTool.CreateCorrection(errors) },
                },
            });
        }
    }

    private async Task<Message> CreateMessageAsync(List<MessageParam> messages, CancellationToken cancellationToken)
    {
        var settings = options.Value;

        try
        {
            return await client.Messages.Create(
                new MessageCreateParams
                {
                    Model = settings.Model,
                    MaxTokens = settings.MaxTokens,
                    System = RecordAnalysisTool.SystemPrompt,
                    Messages = [.. messages],
                    Tools = [RecordAnalysisTool.Definition],
                    ToolChoice = new ToolChoiceTool(RecordAnalysisTool.Name),
                },
                cancellationToken);
        }
        catch (Exception exception) when (exception is AnthropicBadRequestException or AnthropicUnprocessableEntityException)
        {
            // The request itself was rejected, e.g. the document is too long: retrying cannot help.
            throw new UnprocessableDocumentException("The AI service rejected the document.", exception);
        }
    }

    private static ToolUseBlock FindToolUse(Message response)
    {
        foreach (var block in response.Content)
        {
            if (block.TryPickToolUse(out var toolUse) && toolUse.Name == RecordAnalysisTool.Name)
            {
                return toolUse;
            }
        }

        throw new UnprocessableDocumentException(
            response.StopReason == StopReason.Refusal
                ? "The AI declined to analyze the document."
                : "The AI did not return an analysis.");
    }

    private List<string> Parse(IReadOnlyDictionary<string, JsonElement> input, out AnalysisResult? result)
    {
        try
        {
            result = JsonSerializer.Deserialize<AnalysisResult>(JsonSerializer.Serialize(input), _toolInputOptions);
        }
        catch (JsonException exception)
        {
            result = null;
            return [$"The input does not match the tool schema: {exception.Message}"];
        }

        if (result is null)
        {
            return ["The input is empty."];
        }

        return validator.Validate(result).Errors
            .Select(failure => $"{failure.PropertyName}: {failure.ErrorMessage}")
            .ToList();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Claude returned an invalid analysis (attempt {Attempt}): {Errors}")]
    private static partial void LogInvalidAnalysis(ILogger logger, int attempt, string errors);
}
