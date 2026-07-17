namespace UserService.Domain.Users;

using UserService.Domain.Common;

/// <summary>
/// Value Object for an operator/admin's full name (HU-23, Identity &amp; Access).
///
/// Encapsulates the "first and last name are mandatory" rule that previously
/// lived inline in <c>CreateUserCommandHandler</c>, and trims both parts so the
/// stored representation is clean.
/// </summary>
public sealed record PersonName
{
    public string FirstName { get; }
    public string LastName { get; }

    private PersonName(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>
    /// Builds a validated <see cref="PersonName"/> or fails with
    /// <see cref="UserErrors.InvalidName"/> when either part is blank.
    /// </summary>
    public static Result<PersonName> Create(string? firstName, string? lastName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName))
            return Result.Failure<PersonName>(UserErrors.InvalidName);

        return Result.Success(new PersonName(firstName.Trim(), lastName.Trim()));
    }

    public override string ToString() => $"{FirstName} {LastName}";
}
