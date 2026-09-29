namespace DocumentIntelligence.Application.Abstractions.Results;

/// <summary>
/// Outcome of a use case: either a value or an expected <see cref="Error"/>.
/// </summary>
public sealed class Result<TValue>
{
    private readonly TValue? _value;

    private Result(TValue value)
    {
        _value = value;
        IsSuccess = true;
    }

    private Result(Error error)
    {
        Error = error;
        IsSuccess = false;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error? Error { get; }

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static Result<TValue> Success(TValue value) => new(value);

    public static Result<TValue> Failure(Error error) => new(error);

    public static implicit operator Result<TValue>(TValue value) => Success(value);

    public static implicit operator Result<TValue>(Error error) => Failure(error);
}
