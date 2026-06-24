namespace SessionService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.Sessions.Commands.AutoStartTeam;
using SessionService.Application.Sessions.Commands.BroadcastOperatorMessage;
using SessionService.Application.Sessions.Commands.CancelSession;
using SessionService.Application.Sessions.Commands.CreateSession;
using SessionService.Application.Sessions.Commands.FinalizeSession;
using SessionService.Application.Sessions.Commands.ForceAdvanceTeam;
using SessionService.Application.Sessions.Commands.LeaveTeam;
using SessionService.Application.Sessions.Commands.PauseSession;
using SessionService.Application.Sessions.Commands.PenalizeTeam;
using SessionService.Application.Sessions.Commands.ReleaseClue;
using SessionService.Application.Sessions.Commands.ResumeSession;
using SessionService.Application.Sessions.Commands.StartSession;
using SessionService.Application.Sessions.Commands.SubmitTriviaAnswer;
using SessionService.Application.Sessions.Commands.UpdateSession;
using SessionService.Application.Sessions.Commands.ValidateQrCode;
using SessionService.Application.Sessions.Queries.GetParticipantStage;
using SessionService.Application.Sessions.Queries.GetReleasedClues;
using SessionService.Application.Sessions.Queries.GetSessionByCode;
using SessionService.Application.Sessions.Queries.GetSessionAudit;
using SessionService.Application.Sessions.Queries.GetSessionCommandAudit;
using SessionService.Application.Sessions.Queries.GetSessionDashboard;
using SessionService.Application.Sessions.Queries.GetSessionDetail;
using SessionService.Application.Sessions.Queries.GetSessionRanking;
using SessionService.Application.Sessions.Queries.GetSessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Auth;

[ApiController]
[Route("api/[controller]")]
[Authorize] // HU-23: requires Keycloak Bearer token by default; participant-facing
            // endpoints are explicitly marked [AllowAnonymous] below.
public class SessionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(ISender sender, ILogger<SessionsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>
    /// Returns the operator display name extracted from the JWT, used by the
    /// audit log (HU-22). Returns null on anonymous endpoints — the audit
    /// handlers fall back to "Sistema" in that case.
    /// </summary>
    private string? GetOperatorName() => User.GetOperatorDisplayName();

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? missionId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetSessionsQuery(missionId, status), cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetAll), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>Participant entry point: look up a session by its access code.</summary>
    [HttpGet("by-code/{code}")]
    [AllowAnonymous] // HU-17: participantes anónimos
    public async Task<IActionResult> GetByCode(string code, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetSessionByCodeQuery(code), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetByCode), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetSessionDetailQuery(id), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetById), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpGet("{id:guid}/dashboard")]
    public async Task<IActionResult> GetDashboard(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetSessionDashboardQuery(id), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetDashboard), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// Returns the live ranking for a session (HU-21).
    /// Reads the optimized projection from TeamService through the orchestrator
    /// so participants can call SessionService instead of TeamService directly.
    /// </summary>
    [HttpGet("{id:guid}/ranking")]
    [AllowAnonymous] // HU-21: visible para Admin, Operador y Participante
    public async Task<IActionResult> GetRanking(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetSessionRankingQuery(id), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetRanking), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// Returns the full audit timeline of a session (HU-22). Used by Operator
    /// and Administrator to review every clue release, penalty, forced advance
    /// and state change with the exact timestamp and the actor that triggered it.
    /// </summary>
    [HttpGet("{id:guid}/audit")]
    public async Task<IActionResult> GetAudit(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetSessionAuditQuery(id), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetAudit), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// HU-26 — technical command audit log for a session.
    /// Same underlying event store as <c>/audit</c> but the projection includes
    /// the CQRS command type, outcome and millisecond-precise timestamp so the
    /// admin/operator can reconstruct the exact sequence of commands that drove
    /// the session to its current state.
    /// </summary>
    [HttpGet("{id:guid}/audit-log")]
    public async Task<IActionResult> GetCommandAudit(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetSessionCommandAuditQuery(id), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetCommandAudit), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new CancelSessionCommand(id, GetOperatorName()), cancellationToken);

            if (result.IsFailure)
            {
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            }

            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Cancel), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(
                new UpdateSessionCommand(id, request.Name, request.ScheduledAt, GetOperatorName()),
                cancellationToken);

            if (result.IsFailure)
            {
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            }

            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Update), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(
                new CreateSessionCommand(request.MissionId, request.Name, request.ScheduledAt, GetOperatorName()),
                cancellationToken);

            return result.IsSuccess
                ? Ok(result.Value)
                : BadRequest(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Create), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPatch("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new StartSessionCommand(id, GetOperatorName()), cancellationToken);
            if (result.IsFailure)
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Start), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPatch("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new PauseSessionCommand(id, GetOperatorName()), cancellationToken);
            if (result.IsFailure)
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Pause), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPatch("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new ResumeSessionCommand(id, GetOperatorName()), cancellationToken);
            if (result.IsFailure)
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Resume), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPatch("{id:guid}/finalize")]
    public async Task<IActionResult> Finalize(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new FinalizeSessionCommand(id, GetOperatorName()), cancellationToken);
            if (result.IsFailure)
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Finalize), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// HU-28 — pushes a short text message from the operator to every
    /// participant connected to the session. The action is audited.
    /// </summary>
    [HttpPost("{id:guid}/broadcast-message")]
    public async Task<IActionResult> BroadcastOperatorMessage(
        Guid id,
        [FromBody] BroadcastOperatorMessageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(
                new BroadcastOperatorMessageCommand(id, request.Message, GetOperatorName()),
                cancellationToken);

            if (result.IsFailure)
            {
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            }
            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(BroadcastOperatorMessage), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPost("{id:guid}/teams/{teamId:guid}/release-clue")]
    public async Task<IActionResult> ReleaseClue(
        Guid id,
        Guid teamId,
        [FromBody] ReleaseClueRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(
                new ReleaseClueCommand(
                    id,
                    teamId,
                    request.TotalCluesForStage,
                    request.ClueContent,
                    request.ClueLatitude,
                    request.ClueLongitude,
                    request.ClueRadiusMeters,
                    GetOperatorName()),
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ReleaseClue), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPost("{id:guid}/teams/{teamId:guid}/penalize")]
    public async Task<IActionResult> PenalizeTeam(
        Guid id,
        Guid teamId,
        [FromBody] PenalizeTeamRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(
                new PenalizeTeamCommand(id, teamId, request.Points, request.Reason, GetOperatorName()),
                cancellationToken);

            if (result.IsFailure)
            {
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            }

            return Ok(new { newScore = result.Value });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(PenalizeTeam), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPost("{id:guid}/teams/{teamId:guid}/force-advance")]
    public async Task<IActionResult> ForceAdvanceTeam(
        Guid id,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new ForceAdvanceTeamCommand(id, teamId, GetOperatorName()), cancellationToken);

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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ForceAdvanceTeam), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// Returns the current stage data for a participant team.
    /// Strips IsCorrect from options before returning.
    /// </summary>
    [HttpGet("{id:guid}/participant-stage/{teamId:guid}")]
    [AllowAnonymous] // participantes anónimos
    public async Task<IActionResult> GetParticipantStage(
        Guid id,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        try
        {
            // El auto-arranque del equipo es una ESCRITURA: vive en el lado de comandos.
            // Se ejecuta (idempotente, best-effort) antes del query, que es lectura pura.
            await _sender.Send(new AutoStartTeamCommand(id, teamId), cancellationToken);

            var result = await _sender.Send(new GetParticipantStageQuery(id, teamId), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            }
            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetParticipantStage), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// Submits a trivia answer for a participant team.
    /// Rejects with 400 when the session is Paused/Completed/Cancelled.
    /// </summary>
    [HttpPost("{id:guid}/teams/{teamId:guid}/answer-trivia")]
    [AllowAnonymous] // HU-18: participantes responden trivias sin login
    public async Task<IActionResult> SubmitTriviaAnswer(
        Guid id,
        Guid teamId,
        [FromBody] SubmitTriviaAnswerRequest request,
        CancellationToken cancellationToken)
    {
        try
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(SubmitTriviaAnswer), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// Returns the ordered list of clues that have already been released to a team
    /// for its current stage. Used by participants to re-sync after a SignalR reconnect (HU-20).
    /// </summary>
    [HttpGet("{id:guid}/teams/{teamId:guid}/released-clues")]
    [AllowAnonymous] // HU-20: participantes resincronizan tras reconexión
    public async Task<IActionResult> GetReleasedClues(
        Guid id,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetReleasedCluesQuery(id, teamId), cancellationToken);
            if (result.IsFailure)
            {
                return result.Error.Code == SessionErrors.NotFound.Code
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            }
            return Ok(result.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetReleasedClues), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// Validates the QR code scanned by a participant team for a Treasure Hunt stage.
    /// Wrong codes do not advance the team and do not award points (HU-19).
    /// </summary>
    [HttpPost("{id:guid}/teams/{teamId:guid}/validate-qr")]
    [AllowAnonymous] // HU-19: participantes anónimos escanean el QR
    public async Task<IActionResult> ValidateQr(
        Guid id,
        Guid teamId,
        [FromBody] ValidateQrRequest request,
        CancellationToken cancellationToken)
    {
        try
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ValidateQr), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// Participant action: leaves their team. Forwards to TeamService through the
    /// orchestrator — participants never call TeamService directly.
    /// </summary>
    [HttpPost("{id:guid}/teams/{teamId:guid}/leave")]
    [AllowAnonymous] // participantes anónimos
    public async Task<IActionResult> LeaveTeam(
        Guid id,
        Guid teamId,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new LeaveTeamCommand(id, teamId), cancellationToken);
            return result.IsSuccess ? Ok() : BadRequest(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(LeaveTeam), nameof(SessionsController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }
}

public record CreateSessionRequest(Guid MissionId, string Name, DateTime? ScheduledAt);
public record UpdateSessionRequest(string Name, DateTime? ScheduledAt);
public record ReleaseClueRequest(
    int TotalCluesForStage,
    string? ClueContent = null,
    double? ClueLatitude = null,
    double? ClueLongitude = null,
    int? ClueRadiusMeters = null);
public record PenalizeTeamRequest(int Points, string Reason);
public record SubmitTriviaAnswerRequest(Guid StageId, Guid OptionId);
public record ValidateQrRequest(Guid StageId, string ScannedCode);
public record BroadcastOperatorMessageRequest(string Message);
