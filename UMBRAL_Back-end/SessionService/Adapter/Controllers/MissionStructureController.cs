namespace SessionService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.Missions.Queries.GetMissionStructure;

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

    public MissionStructureController(ISender sender) => _sender = sender;

    [HttpGet("{missionId:guid}/structure")]
    public async Task<IActionResult> GetStructure(Guid missionId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMissionStructureQuery(missionId), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }
}
