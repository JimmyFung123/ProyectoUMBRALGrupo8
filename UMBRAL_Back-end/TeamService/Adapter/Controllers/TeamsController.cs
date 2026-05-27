namespace TeamService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using TeamService.Application.Teams.Commands.AnswerTrivia;
using TeamService.Application.Teams.Commands.CreateTeam;
using TeamService.Application.Teams.Commands.ForceAdvance;
using TeamService.Application.Teams.Commands.JoinTeam;
using TeamService.Application.Teams.Commands.PenalizeTeam;
using TeamService.Application.Teams.Commands.ReleaseClue;
using TeamService.Application.Teams.Queries.GetSessionRanking;
using TeamService.Application.Teams.Queries.GetTeamById;
using TeamService.Application.Teams.Queries.GetTeamProgress;
using TeamService.Domain.Teams;

[ApiController]
[Route("api/[controller]")]
public class TeamsController : ControllerBase
{
    private readonly ISender _sender;

    public TeamsController(ISender sender) => _sender = sender;

    /// <summary>Creates a new team for a session. Called by the team leader.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CreateTeamCommand(request.SessionId, request.TeamName), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    /// <summary>Joins an existing team using the team's invite code.</summary>
    [HttpPost("{inviteCode}/join")]
    public async Task<IActionResult> Join(
        string inviteCode,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new JoinTeamCommand(inviteCode), cancellationToken);
        if (result.IsFailure)
            return result.Error.Code == TeamErrors.TeamNotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return Ok(result.Value);
    }

    /// <summary>Returns basic info for a single team by ID (used by participant waiting room).</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetTeamByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

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
    /// Returns the live ranking for a session (HU-21). Optimized read model:
    /// score descending, with resolution-time (LastStageCompletedAt) as tie-breaker.
    /// Safe to call from operator dashboards and the participant app — no write side-effects.
    /// </summary>
    [HttpGet("ranking")]
    public async Task<IActionResult> GetSessionRanking(
        [FromQuery] Guid sessionId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionRankingQuery(sessionId), cancellationToken);
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

    /// <summary>
    /// Forces a team to advance to the specified next stage, earning 0 points for the skipped stage.
    /// HU-25: response carries <c>elapsedSeconds</c> so SessionService can record the
    /// analytics fact row.
    /// </summary>
    [HttpPost("{id:guid}/force-advance")]
    public async Task<IActionResult> ForceAdvance(
        Guid id,
        [FromBody] ForceAdvanceTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ForceAdvanceTeamCommand(id, request.NextStageOrder), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code == TeamErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }
        return Ok(new
        {
            newScore = result.Value.NewScore,
            elapsedSeconds = result.Value.ElapsedSeconds,
        });
    }

    /// <summary>
    /// Records a trivia answer for a team: adjusts score and advances to the next stage.
    /// Called by SessionService only — never called directly by participants.
    /// HU-25: response carries <c>elapsedSeconds</c> so SessionService can record
    /// the analytics fact row for the stage just completed.
    /// </summary>
    [HttpPost("{id:guid}/answer-trivia")]
    public async Task<IActionResult> AnswerTrivia(
        Guid id,
        [FromBody] AnswerTriviaRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new AnswerTriviaCommand(id, request.IsCorrect, request.ScoreChange, request.NextStageOrder),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == TeamErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(new
        {
            newScore = result.Value.NewScore,
            elapsedSeconds = result.Value.ElapsedSeconds,
        });
    }
}

public record ReleaseClueRequest(int TotalCluesForStage, bool IsAutomatic = false);
public record PenalizeTeamRequest(int Points, string Reason);
public record ForceAdvanceTeamRequest(int NextStageOrder);
public record AnswerTriviaRequest(bool IsCorrect, int ScoreChange, int NextStageOrder);
public record CreateTeamRequest(Guid SessionId, string TeamName);
