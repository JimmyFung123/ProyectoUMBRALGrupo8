namespace SessionService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.Missions.Queries.GetMissionStructure;
using SessionService.Domain.Common;

/// <summary>
/// Operator-facing preview of a mission's full structure (stages → clues),
/// assembled in SessionService from the StageService and ClueService feeds and
/// modelled with the Composite pattern.
/// </summary>
[ApiController]
[Route("api/missions")]
[Authorize] // operator tooling — requires a Keycloak Bearer token
public class MissionStructureController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<MissionStructureController> _logger;

    public MissionStructureController(ISender sender, ILogger<MissionStructureController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet("{missionId:guid}/structure")]
    public async Task<IActionResult> GetStructure(Guid missionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetMissionStructureQuery(missionId), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetStructure), nameof(MissionStructureController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }
}
