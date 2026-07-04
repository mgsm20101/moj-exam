namespace ExamSystem.Application.Common.Models;

/// <summary>Represents the outcome of an operation without relying on exceptions for expected/business failures.</summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    private Result(bool isSuccess, T? value, IReadOnlyList<string> errors)
    {
        IsSuccess = isSuccess;
        Value = value;
        Errors = errors;
    }

    public static Result<T> Success(T value) => new(true, value, Array.Empty<string>());

    public static Result<T> Failure(params string[] errors) => new(false, default, errors);

    public static Result<T> Failure(IEnumerable<string> errors) => new(false, default, errors.ToList());
}
