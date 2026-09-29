using DocumentIntelligence.Domain.Abstractions;

namespace DocumentIntelligence.Domain.Documents.Analysis;

public enum RiskSeverity
{
    Low,
    Medium,
    High,
}

public sealed record Risk
{
    public Risk(RiskSeverity severity, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("Risk description must not be empty.");
        }

        Severity = severity;
        Description = description.Trim();
    }

    public RiskSeverity Severity { get; }

    public string Description { get; }
}
