namespace DocumentIntelligence.Application.Abstractions.Results;

/// <summary>
/// Validation failures grouped by property name.
/// </summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error("Validation.Failed", "One or more validation errors occurred.", ErrorType.Validation);
