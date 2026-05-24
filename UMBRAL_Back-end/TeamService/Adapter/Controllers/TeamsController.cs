namespace TeamService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using TeamService.Application.Teams.Commands.PenalizeTeam;
using TeamService.Application.Teams.Commands.ReleaseClue;
using TeamService.Application.Teams.Queries.GetTeamProgress;
using TeamService.Domain.Teams;

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

    /// <summary>
    /// Records the release of the next sequential clue to a team.
    /// Fails with 409 when all configured clues for the stage have already been released.
    /// </summary>
    [HttpPost("{id:guid}/release-clue")]
    public async Task<IActionResult> ReleaseClue(
        Guid id,
        [FromBody] ReleaseClueRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ReleaseClueCommand(id, request.TotalCluesForStage, request.IsAutomatic), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code == TeamErrors.NotFound.Code
                ? NotFound(result.Error)
                : Conflict(result.Error);
        }
        return Ok(new { cluesReceived = result.Value });
    }

    [HttpPost("{id:guid}/penalize")]
    public async Task<IActionResult> Penalize(
        Guid id,
        [FromBody] PenalizeTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PenalizeTeamCommand(id, request.Points, request.Reason), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code == TeamErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }
        return Ok(new { newScore = result.Value });
    }
}

public record ReleaseClueRequest(int TotalCluesForStage, bool IsAutomatic = false);
public record PenalizeTeamRequest(int Points, string Reason);
