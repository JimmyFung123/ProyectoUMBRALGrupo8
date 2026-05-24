namespace TeamService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using TeamService.Application.Teams.Queries.GetTeamProgress;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ISender _sender;

    public TeamsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Returns all teams for a session, ranked by score (highest first).
    /// Returns an empty array when no teams are enrolled.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTeamProgress(
        [FromQuery] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTeamProgressQuery(sessionId), cancellationToken);
        return Ok(result);
    }
}
