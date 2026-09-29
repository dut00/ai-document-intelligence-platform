using DocumentIntelligence.Application.Abstractions.Results;

namespace DocumentIntelligence.Api.Extensions;

internal static class ResultExtensions
{
    /// <summary>
    /// Maps an expected business error to an RFC 9457 problem response.
    /// </summary>
    public static IResult ToProblem(this Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error is ValidationError validation)
        {
            return TypedResults.ValidationProblem(
                validation.Errors.ToDictionary(),
                title: validation.Description,
                extensions: extensions);
        }

        return TypedResults.Problem(
            title: error.Description,
            statusCode: error.Type switch
            {
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status400BadRequest,
            },
            extensions: extensions);
    }
}
