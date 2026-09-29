using System.ComponentModel.DataAnnotations;

namespace DocumentIntelligence.Infrastructure.Messaging;

/// <summary>
/// Exponential in-process retry of a failing consumer. Once the retries are exhausted the message
/// moves to the endpoint's <c>_error</c> queue and a <c>Fault&lt;T&gt;</c> is published.
/// </summary>
public sealed class MessageRetryOptions
{
    public const string SectionName = "Messaging:Retry";

    [Range(0, 20)]
    public int RetryLimit { get; set; } = 5;

    public TimeSpan MinInterval { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan IntervalDelta { get; set; } = TimeSpan.FromSeconds(2);
}
