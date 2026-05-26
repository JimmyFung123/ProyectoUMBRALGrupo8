namespace UserService.Application.Users.Commands.CreateUser;

using System.Text.RegularExpressions;
using MediatR;
using UserService.Domain.Common;
using UserService.Domain.Users;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    // Loose RFC-5322 check — Keycloak does its own validation on top.
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

    private readonly IKeycloakAdminClient _keycloak;

    public CreateUserCommandHandler(IKeycloakAdminClient keycloak) => _keycloak = keycloak;

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // ── Input validation ──────────────────────────────────────────────────
        var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!EmailRegex.IsMatch(email))
            return Result.Failure<Guid>(UserErrors.InvalidEmail);

        if (string.IsNullOrWhiteSpace(request.FirstName) || string.IsNullOrWhiteSpace(request.LastName))
            return Result.Failure<Guid>(UserErrors.InvalidName);

        if (string.IsNullOrWhiteSpace(request.TemporaryPassword) || request.TemporaryPassword.Length < 8)
            return Result.Failure<Guid>(UserErrors.InvalidPassword);

        // ── HU-23 Criterio 1 / Flujo alterno: email único ────────────────────
        var existing = await _keycloak.FindByEmailAsync(email, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(UserErrors.EmailAlreadyInUse);

        try
        {
            var id = await _keycloak.CreateUserAsync(
                email,
                request.FirstName.Trim(),
                request.LastName.Trim(),
                request.TemporaryPassword,
                request.Role,
                cancellationToken);

            return Result.Success(id);
        }
        catch (KeycloakConflictException)
        {
            // Race condition: another admin created the same email between
            // our pre-check and the POST. Keycloak's 409 wins.
            return Result.Failure<Guid>(UserErrors.EmailAlreadyInUse);
        }
        catch
        {
            return Result.Failure<Guid>(UserErrors.KeycloakUnavailable);
        }
    }
}
