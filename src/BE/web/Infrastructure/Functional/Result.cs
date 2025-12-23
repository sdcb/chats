using System.Diagnostics.CodeAnalysis;

namespace Chats.BE.Infrastructure.Functional;

public record Result
{
    public required bool IsSuccess { get; init; }
    public required string? Error { get; init; }
    public bool IsFailure => !IsSuccess;

    [SetsRequiredMembers]
    protected Result(bool isSuccess, string? error)
    {
        if (isSuccess && error != null)
            throw new InvalidOperationException();
        if (!isSuccess && error == null)
            throw new InvalidOperationException();

        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Fail(string message)
    {
        return new Result(false, message);
    }

    public static Result<T> Fail<T>(string message)
    {
        return new Result<T>(default!, false, message);
    }

    public static Result Ok()
    {
        return new Result(true, null);
    }

    public static Result<T> Ok<T>(T value)
    {
        return new Result<T>(value, true, null);
    }

    public static Result Combine(IEnumerable<Result> results)
    {
        foreach (Result result in results.Where(x => x != null))
        {
            if (result.IsFailure)
                return result;
        }

        return Ok();
    }

    public static Result Combine(params Result[] results)
    {
        return Combine((IEnumerable<Result>)results);
    }
}


public record Result<T> : Result
{
    private readonly T _value;

    public T Value
    {
        get
        {
            if (!IsSuccess)
                throw new ResultFailedException(Error!);

            return _value;
        }
    }

    [SetsRequiredMembers]
    protected internal Result(T value, bool isSuccess, string? error)
        : base(isSuccess, error)
    {
        _value = value;
    }
}