namespace UserService.Domain.Users;

using UserService.Domain.Common;

public sealed record Password
{
    public string Value { get; }

    private Password(string value) => Value = value;

    public static Result<Password> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length < 8)
            return Result.Failure<Password>(UserErrors.InvalidPassword);

        return Result.Success(new Password(raw));
    }
}
