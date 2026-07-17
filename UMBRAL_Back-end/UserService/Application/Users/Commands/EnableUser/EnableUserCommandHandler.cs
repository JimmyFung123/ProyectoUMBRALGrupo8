namespace UserService.Application.Users.Commands.EnableUser;

using MediatR;
using UserService.Domain.Common;
using UserService.Domain.Users;

public class EnableUserCommandHandler : IRequestHandler<EnableUserCommand, Result>
{
    private readonly IKeycloakAdminClient _keycloak;

    public EnableUserCommandHandler(IKeycloakAdminClient keycloak) => _keycloak = keycloak;

    public async Task<Result> Handle(EnableUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _keycloak.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(UserErrors.NotFound);

        // Idempotente: si ya está activo, no hacemos nada.
        if (user.Enabled)
            return Result.Success();

        try
        {
            await _keycloak.EnableAsync(request.UserId, cancellationToken);
            return Result.Success();
        }
        catch
        {
            return Result.Failure(UserErrors.KeycloakUnavailable);
        }
    }
}
