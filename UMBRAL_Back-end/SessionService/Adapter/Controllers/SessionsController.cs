namespace SessionService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.Sessions.Commands.CancelSession;
using SessionService.Application.Sessions.Commands.CreateSession;
using SessionService.Application.Sessions.Commands.FinalizeSession;
using SessionService.Application.Sessions.Commands.ForceAdvanceTeam;
using SessionService.Application.Sessions.Commands.PauseSession;
using SessionService.Application.Sessions.Commands.PenalizeTeam;
using SessionService.Application.Sessions.Commands.ReleaseClue;
using SessionService.Application.Sessions.Commands.ResumeSession;
using SessionService.Application.Sessions.Commands.StartSession;
using SessionService.Application.Sessions.Commands.SubmitTriviaAnswer;
using SessionService.Application.Sessions.Commands.UpdateSession;
using SessionService.Application.Sessions.Commands.ValidateQrCode;
using SessionService.Application.Sessions.Queries.GetParticipantStage;
using SessionService.Application.Sessions.Queries.GetSessionByCode;
using SessionService.Application.Sessions.Queries.GetSessionDashboard;
using SessionService.Application.Sessions.Queries.GetSessionDetail;
using SessionService.Application.Sessions.Queries.GetSessions;
using SessionService.Domain.Sessions;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISender _sender;

    public SessionsController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? missionId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionsQuery(missionId, status), cancellationToken);
        return Ok(result);
    }

    /// <summary>Participant entry point: look up a session by its access code.</summary>
    [HttpGet("by-code/{code}")]
    public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionByCodeQuery(code), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionDetailQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpGet("{id:guid}/dashboard")]
    public async Task<IActionResult> GetDashboard(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionDashboardQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new CancelSessionCommand(id), cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateSessionCommand(id, request.Name, request.ScheduledAt),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateSessionCommand(request.MissionId, request.Name, request.ScheduledAt),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : BadRequest(result.Error);
    }

    [HttpPatch("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new StartSessionCommand(id), cancellationToken);
        if (result.IsFailure)
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return Ok(result.Value);
    }

    [HttpPatch("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new PauseSessionCommand(id), cancellationToken);
        if (result.IsFailure)
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return Ok(result.Value);
    }

    [HttpPatch("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ResumeSessionCommand(id), cancellationToken);
        if (result.IsFailure)
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return Ok(result.Value);
    }

    [HttpPatch("{id:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new FinalizeSessionCommand(id), cancellationToken);
        if (result.IsFailure)
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/teams/{teamId:guid}/release-clue")]
    public async Task<IActionResult> ReleaseClue(
        Guid id,
        Guid teamId,
        [FromBody] ReleaseClueRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ReleaseClueCommand(id, teamId, request.TotalCluesForStage, request.ClueContent),
            cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == SessionErrors.NotFound.Code)
                return NotFound(result.Error);
            if (result.Error.Code == SessionErrors.AllCluesAlreadyReleased.Code)
                return Conflict(result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("{id:guid}/teams/{teamId:guid}/penalize")]
    public async Task<IActionResult> PenalizeTeam(
        Guid id,
        Guid teamId,
        [FromBody] PenalizeTeamRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new PenalizeTeamCommand(id, teamId, request.Points, request.Reason),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(new { newScore = result.Value });
    }

    [HttpPost("{id:guid}/teams/{teamId:guid}/force-advance")]
    public async Task<IActionResult> ForceAdvanceTeam(
        Guid id,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new ForceAdvanceTeamCommand(id, teamId), cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code == SessionErrors.NotFound.Code)
                return NotFound(result.Error);
            if (result.Error.Code == SessionErrors.TeamAlreadyOnLastStage.Code)
                return Conflict(result.Error);
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Returns the current stage data for a participant team.
    /// Strips IsCorrect from options before returning.
    /// </summary>
    [HttpGet("{id:guid}/participant-stage/{teamId:guid}")]
    public async Task<IActionResult> GetParticipantStage(
        Guid id,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetParticipantStageQuery(id, teamId), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }
        return Ok(result.Value);
    }

    /// <summary>
    /// Submits a trivia answer for a participant team.
    /// Rejects with 400 when the session is Paused/Completed/Cancelled.
    /// </summary>
    [HttpPost("{id:guid}/teams/{teamId:guid}/answer-trivia")]
    public async Task<IActionResult> SubmitTriviaAnswer(
        Guid id,
        Guid teamId,
        [FromBody] SubmitTriviaAnswerRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new SubmitTriviaAnswerCommand(id, teamId, request.StageId, request.OptionId),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Validates the QR code scanned by a participant team for a Treasure Hunt stage.
    /// Wrong codes do not advance the team and do not award points (HU-19).
    /// </summary>
    [HttpPost("{id:guid}/teams/{teamId:guid}/validate-qr")]
    public async Task<IActionResult> ValidateQr(
        Guid id,
        Guid teamId,
        [FromBody] ValidateQrRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ValidateQrCodeCommand(id, teamId, request.StageId, request.ScannedCode),
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error.Code == SessionErrors.NotFound.Code
                ? NotFound(result.Error)
                : BadRequest(result.Error);
        }

        return Ok(result.Value);
    }
}

public record CreateSessionRequest(Guid MissionId, string Name, DateTime? ScheduledAt);
public record UpdateSessionRequest(string Name, DateTime? ScheduledAt);
public record ReleaseClueRequest(int TotalCluesForStage, string ClueContent);
public record PenalizeTeamRequest(int Points, string Reason);
public record SubmitTriviaAnswerRequest(Guid StageId, Guid OptionId);
public record ValidateQrRequest(Guid StageId, string ScannedCode);
