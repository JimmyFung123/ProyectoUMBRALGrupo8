namespace UserService.Application.Users.Commands.SendTemporaryPassword;

using MediatR;
using Microsoft.Extensions.Logging;
using UserService.Domain.Common;
using UserService.Domain.Users;

public class SendTemporaryPasswordCommandHandler : IRequestHandler<SendTemporaryPasswordCommand, Result>
{
    private readonly IKeycloakAdminClient _keycloak;
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IUserEmailSender _emailSender;
    private readonly ILogger<SendTemporaryPasswordCommandHandler> _logger;

    public SendTemporaryPasswordCommandHandler(
        IKeycloakAdminClient keycloak,
        IPasswordGenerator passwordGenerator,
        IUserEmailSender emailSender,
        ILogger<SendTemporaryPasswordCommandHandler> logger)
    {
        _keycloak = keycloak;
        _passwordGenerator = passwordGenerator;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task<Result> Handle(SendTemporaryPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _keycloak.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(UserErrors.NotFound);

        var temporaryPassword = _passwordGenerator.Generate();

        try
        {
            await _keycloak.ResetPasswordAsync(request.UserId, temporaryPassword, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo resetear la clave en Keycloak para el usuario {UserId}.", request.UserId);
            return Result.Failure(UserErrors.KeycloakUnavailable);
        }

        try
        {
            await _emailSender.SendTemporaryPasswordAsync(
                user.Email, user.FirstName, temporaryPassword, cancellationToken);
        }
        catch (Exception ex)
        {
            // La clave ya fue restablecida en Keycloak; el fallo de correo
            // se informa al administrador para que tome acción manualmente.
            _logger.LogError(ex,
                "Clave reseteada para {Email}, pero falló el envío del correo.", user.Email);
            return Result.Failure(UserErrors.EmailDeliveryFailed);
        }

        return Result.Success();
    }
}
