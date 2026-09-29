using System.ComponentModel.DataAnnotations;

namespace DocumentIntelligence.Infrastructure.Analysis;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    /// <summary>
    /// Fallback for <see cref="ApiKey"/>: the variable the Anthropic tooling uses.
    /// </summary>
    public const string ApiKeyVariable = "ANTHROPIC_API_KEY";

    /// <summary>
    /// Without a key the deterministic fake analyzer is used instead of Claude.
    /// </summary>
    public string? ApiKey { get; set; }

    [Required]
    public string Model { get; set; } = "claude-haiku-4-5-20251001";

    [Range(256, 32_000)]
    public int MaxTokens { get; set; } = 4096;

    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Retries of rate-limited and failed requests inside the SDK, before the message itself is retried.
    /// </summary>
    [Range(0, 10)]
    public int MaxRetries { get; set; } = 2;
}
