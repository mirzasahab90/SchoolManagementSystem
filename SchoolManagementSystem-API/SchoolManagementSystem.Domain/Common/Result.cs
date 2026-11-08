namespace SchoolManagementSystem.Domain.Common;

/// <summary>
/// The outcome of a service call. Expected failures are returned as an <see cref="Error"/>
/// instead of being thrown.
/// </summary>
public class Result
{
    protected Result(Error? error, string? message = null)
    {
        Error = error;
        Message = message;
    }

    public Error? Error { get; }

    public string? Message { get; }

    public bool IsSuccess => Error is null;

    public static Result Ok(string? message = null) => new(null, message);

    public static Result Fail(Error error) => new(error);

    public static implicit operator Result(Error error) => new(error);
}

public sealed class Result<T> : Result
{
    private Result(T value) : base(null) => Value = value;

    private Result(Error error) : base(error)
    {
    }

    public T? Value { get; }

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(Error error) => new(error);
}
