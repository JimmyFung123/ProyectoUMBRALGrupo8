namespace UserService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UMBRAL.Auth;
using UserService.Application.Users.Commands.ChangeRole;
using UserService.Application.Users.Commands.CreateUser;
using UserService.Application.Users.Commands.DisableUser;
using UserService.Application.Users.Commands.EnableUser;
using UserService.Application.Users.Queries.GetUsers;
using UserService.Domain.Common;
using UserService.Domain.Users;

/// <summary>
/// HU-23: gestión integral del personal operativo. Todos los endpoints
/// requieren rol de administrador — la gestión de personal nunca se delega
/// a operadores comunes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = OperatorPrincipal.AdminRole)]
public class UsersController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<UsersController> _logger;

    public UsersController(ISender sender, ILogger<UsersController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>Lista todo el personal operativo (administradores y operadores).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var users = await _sender.Send(new GetUsersQuery(), cancellationToken);
            return Ok(users);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetAll), nameof(UsersController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>HU-23 Criterio 1: crea un nuevo usuario con su rol.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(
                new CreateUserCommand(
                    request.Email,
                    request.FirstName,
                    request.LastName,
                    request.Role),
                cancellationToken);

            if (result.IsFailure)
                return MapErrorToStatus(result.Error);

            return CreatedAtAction(nameof(GetAll), new { id = result.Value }, new { id = result.Value });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Create), nameof(UsersController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>HU-23 Criterio 2: cambia el rol del usuario.</summary>
    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        [FromBody] ChangeRoleRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new ChangeRoleCommand(id, request.NewRole), cancellationToken);
            return result.IsSuccess ? NoContent() : MapErrorToStatus(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ChangeRole), nameof(UsersController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>HU-23 Criterio 3+4: deshabilita lógicamente al usuario.</summary>
    [HttpPut("{id:guid}/disable")]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            // Para la regla "no auto-desactivarse" pasamos el email del JWT del
            // administrador que ejecuta la acción. El handler lo compara con el
            // email del usuario destino.
            var requesterEmail = User.FindFirst("email")?.Value
                                  ?? User.FindFirst("preferred_username")?.Value;
            var result = await _sender.Send(new DisableUserCommand(id, requesterEmail), cancellationToken);
            return result.IsSuccess ? NoContent() : MapErrorToStatus(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Disable), nameof(UsersController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPut("{id:guid}/enable")]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new EnableUserCommand(id), cancellationToken);
            return result.IsSuccess ? NoContent() : MapErrorToStatus(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Enable), nameof(UsersController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    private IActionResult MapErrorToStatus(Domain.Common.Error error)
    {
        // Mapeamos códigos de dominio a HTTP statuses razonables para que el
        // front pueda discriminar entre 4xx de negocio y 5xx de infraestructura.
        if (error.Code == UserErrors.NotFound.Code)
            return NotFound(error);
        if (error.Code == UserErrors.EmailAlreadyInUse.Code)
            return Conflict(error);
        if (error.Code == UserErrors.KeycloakUnavailable.Code)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, error);
        return BadRequest(error);
    }
}

public record CreateUserRequest(
    string Email,
    string FirstName,
    string LastName,
    UserRole Role);

public record ChangeRoleRequest(UserRole NewRole);
