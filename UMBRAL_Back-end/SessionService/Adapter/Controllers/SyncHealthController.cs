namespace SessionService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.SyncHealth.Commands.ProxyReprojects;
using SessionService.Application.SyncHealth.Commands.ReconcileStageCompletionRecords;
using SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;
using SessionService.Application.SyncHealth.Queries.GetSyncHealth;
using SessionService.Domain.Common;
using UMBRAL.Auth;

/// <summary>
/// HU-27 — admin-only sync-health dashboard surface.
///
/// Exposes the aggregated view of every CQRS read model in the system and the
/// manual reproject/reconcile actions an administrator can trigger when the
/// dashboard surfaces drift.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = OperatorPrincipal.AdminRole)]
public class SyncHealthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<SyncHealthController> _logger;

    public SyncHealthController(ISender sender, ILogger<SyncHealthController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>Returns the full snapshot rendered by the admin dashboard.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new GetSyncHealthQuery(), ct);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Get), nameof(SyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>
    /// Rebuilds the local <c>MissionsLookup</c> projection from MissionService.
    /// Used when the dashboard shows count drift on the "MissionsLookup
    /// (SessionService)" card.
    /// </summary>
    [HttpPost("projections/missions-lookup-session/reproject")]
    public async Task<IActionResult> ReprojectSessionMissionsLookup(CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new ReprojectSessionMissionsLookupCommand(), ct);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ReprojectSessionMissionsLookup), nameof(SyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>Re-fixes <c>IncludedInStatistics</c> flag on completed sessions.</summary>
    [HttpPost("projections/stage-completion-records/reconcile")]
    public async Task<IActionResult> ReconcileStageCompletionRecords(CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new ReconcileStageCompletionRecordsCommand(), ct);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ReconcileStageCompletionRecords), nameof(SyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>Forwards the reproject to StageService for its MissionsLookup replica.</summary>
    [HttpPost("projections/missions-lookup-stage/reproject")]
    public async Task<IActionResult> ReprojectStageMissionsLookup(CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new ReprojectStageMissionsLookupCommand(), ct);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ReprojectStageMissionsLookup), nameof(SyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>Forwards the reproject to MissionService for its StageCountLookup replica.</summary>
    [HttpPost("projections/stage-count-lookup/reproject")]
    public async Task<IActionResult> ReprojectStageCountLookup(CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new ReprojectStageCountLookupCommand(), ct);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ReprojectStageCountLookup), nameof(SyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>Forwards the reproject to ClueService for its StageLookup replica.</summary>
    [HttpPost("projections/stage-lookup/reproject")]
    public async Task<IActionResult> ReprojectStageLookup(CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new ReprojectStageLookupCommand(), ct);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ReprojectStageLookup), nameof(SyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>Forwards the per-session ranking reproject to TeamService.</summary>
    [HttpPost("projections/ranking-projection/sessions/{sessionId:guid}/reproject")]
    public async Task<IActionResult> ReprojectRankingForSession(Guid sessionId, CancellationToken ct)
    {
        try
        {
            var result = await _sender.Send(new ReprojectRankingForSessionCommand(sessionId), ct);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(ReprojectRankingForSession), nameof(SyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }
}
