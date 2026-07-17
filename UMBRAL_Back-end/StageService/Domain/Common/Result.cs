namespace StageService.Domain.Common;
public class Result<T>
{
    private Result(T value) { Value = value; IsSuccess = true; Error = Error.None; }
    private Result(Error error) { Value = default!; IsSuccess = false; Error = error; }
    public T Value { get; }
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }
    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
}
public static class Result
{
    public static Result<T> Success<T>(T value) => Result<T>.Success(value);
    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);
}
