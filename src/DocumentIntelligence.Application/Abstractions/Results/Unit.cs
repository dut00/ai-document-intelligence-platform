namespace DocumentIntelligence.Application.Abstractions.Results;

/// <summary>
/// The value of a successful command that returns nothing.
/// </summary>
public readonly record struct Unit
{
    public static readonly Unit Value = default;
}
