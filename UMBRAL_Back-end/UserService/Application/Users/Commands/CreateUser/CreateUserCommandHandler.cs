namespace UserService.Application.Users.Commands.CreateUser;

using MediatR;
using UserService.Domain.Common;
using UserService.Domain.Users;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IKeycloakAdminClient _keycloak;

    public CreateUserCommandHandler(IKeycloakAdminClient keycloak) => _keycloak = keycloak;

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // ── Input validation via Value Objects (HU-23) ───────────────────────
        var emailResult = EmailAddress.Create(request.Email);
        if (emailResult.IsFailure)
            return Result.Failure<Guid>(emailResult.Error);
        var email = emailResult.Value;

        var nameResult = PersonName.Create(request.FirstName, request.LastName);
        if (nameResult.IsFailure)
            return Result.Failure<Guid>(nameResult.Error);
        var name = nameResult.Value;

        var passwordResult = Password.Create(request.TemporaryPassword);
        if (passwordResult.IsFailure)
            return Result.Failure<Guid>(passwordResult.Error);
        var password = passwordResult.Value;

        // ── HU-23 Criterio 1 / Flujo alterno: email único ────────────────────
        var existing = await _keycloak.FindByEmailAsync(email.Value, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(UserErrors.EmailAlreadyInUse);

        try
        {
            // Race condition: Keycloak returns 409 if another admin created the same
            // email between our pre-check and the POST. The Result handles it gracefully.
            return await _keycloak.CreateUserAsync(
                email.Value,
                name.FirstName,
                name.LastName,
                password.Value,
                request.Role,
                cancellationToken);
        }
        catch
        {
            return Result.Failure<Guid>(UserErrors.KeycloakUnavailable);
        }
    }
}
