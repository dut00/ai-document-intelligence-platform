namespace DocumentIntelligence.Application.Abstractions.Results;

/// <summary>
/// An expected business error. The API maps <see cref="Type"/> to an HTTP status code.
/// </summary>
public record Error(string Code, string Description, ErrorType Type)
{
    public static Error Failure(string code, string description) => new(code, description, ErrorType.Failure);

    public static Error NotFound(string code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Unauthorized(string code, string description) => new(code, description, ErrorType.Unauthorized);
}
