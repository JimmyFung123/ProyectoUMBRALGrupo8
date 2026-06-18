namespace UserService.Application.Users.Commands.CreateUser;

using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Domain.Common;
using UserService.Domain.Users;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly IKeycloakAdminClient _keycloak;
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IUserEmailSender _emailSender;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IKeycloakAdminClient keycloak,
        IPasswordGenerator passwordGenerator,
        IUserEmailSender emailSender,
        ILogger<CreateUserCommandHandler> logger)
    {
        _keycloak = keycloak;
        _passwordGenerator = passwordGenerator;
        _emailSender = emailSender;
        _logger = logger;
    }

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

        // ── HU-23 Criterio 1 / Flujo alterno: email único ────────────────────
        var existing = await _keycloak.FindByEmailAsync(email.Value, cancellationToken);
        if (existing is not null)
            return Result.Failure<Guid>(UserErrors.EmailAlreadyInUse);

        // El admin NO elige la contraseña: la genera el sistema. Se guarda en
        // Keycloak como temporal (cambio obligado en el primer login).
        var temporaryPassword = _passwordGenerator.Generate();

        Result<Guid> created;
        try
        {
            // Race condition: Keycloak returns 409 if another admin created the same
            // email between our pre-check and the POST. The Result handles it gracefully.
            created = await _keycloak.CreateUserAsync(
                email.Value,
                name.FirstName,
                name.LastName,
                temporaryPassword,
                request.Role,
                cancellationToken);
        }
        catch
        {
            return Result.Failure<Guid>(UserErrors.KeycloakUnavailable);
        }

        if (created.IsFailure)
            return created;

        // El usuario ya existe en Keycloak. El envío del correo es una
        // notificación: si falla, NO revertimos (sería confuso y dejaría el
        // email "ocupado"). Registramos el fallo — el admin puede reenviar las
        // credenciales o resetear la clave desde la consola de Keycloak.
        try
        {
            await _emailSender.SendTemporaryPasswordAsync(
                email.Value, name.FirstName, temporaryPassword, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Usuario {Email} creado, pero falló el envío del correo con la clave temporal.",
                email.Value);
        }

        return created;
    }
}
