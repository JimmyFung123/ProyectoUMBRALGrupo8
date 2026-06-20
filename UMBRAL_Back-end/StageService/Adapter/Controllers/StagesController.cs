namespace StageService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using StageService.Application.Stages.Commands.AddStage;
using StageService.Application.Stages.Commands.RemoveStage;
using StageService.Application.Stages.Commands.SetAutoRelease;
using StageService.Application.Stages.Commands.UpdateStage;
using StageService.Application.Stages.Queries.GetStageById;
using StageService.Application.Stages.Queries.GetStagesByMission;
using StageService.Domain.Common;

[ApiController]
[Route("api/[controller]")]
public class StagesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<StagesController> _logger;

    public StagesController(ISender sender, ILogger<StagesController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetByMission([FromQuery] Guid missionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetStagesByMissionQuery(missionId), cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetByMission), nameof(StagesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    /// <summary>Returns a single stage with all options (including IsCorrect). Called by SessionService only.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetStageByIdQuery(id), cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetById), nameof(StagesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddStageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var options = request.Options?.Select(o => (o.Text, o.IsCorrect)).ToList();
            var command = new AddStageCommand(
                request.MissionId, request.Title, request.Type, request.Order, request.BaseScore,
                request.Question, options, request.Latitude, request.Longitude, request.QrCode);
            var result = await _sender.Send(command, cancellationToken);
            return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Add), nameof(StagesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStageRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var options = request.Options?.Select(o => (o.Text, o.IsCorrect)).ToList();
            var command = new UpdateStageCommand(
                id, request.Title, request.Order, request.BaseScore,
                request.Question, options, request.Latitude, request.Longitude, request.QrCode,
                request.AutoReleaseTimeMinutes, request.AutoReleaseMaxAttempts);
            var result = await _sender.Send(command, cancellationToken);
            return result.IsSuccess ? NoContent() : BadRequest(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Update), nameof(StagesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, [FromQuery] Guid missionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new RemoveStageCommand(id, missionId), cancellationToken);
            return result.IsSuccess ? NoContent() : BadRequest(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Remove), nameof(StagesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPatch("{id:guid}/auto-release")]
    public async Task<IActionResult> SetAutoRelease(Guid id, [FromBody] AutoReleaseRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new SetAutoReleaseCommand(id, request.TimeMinutes, request.MaxAttempts), cancellationToken);
            return result.IsSuccess ? NoContent() : BadRequest(result.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(SetAutoRelease), nameof(StagesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }
}

// ── Request records ───────────────────────────────────────────────────────────

public record OptionRequest(string Text, bool IsCorrect);

public record AddStageRequest(
    Guid MissionId,
    string Title,
    string Type,
    int Order,
    int BaseScore,
    string? Question,
    List<OptionRequest>? Options,
    double? Latitude,
    double? Longitude,
    string? QrCode
);

public record UpdateStageRequest(
    string Title,
    int Order,
    int BaseScore,
    string? Question,
    List<OptionRequest>? Options,
    double? Latitude,
    double? Longitude,
    string? QrCode,
    int? AutoReleaseTimeMinutes,
    int? AutoReleaseMaxAttempts
);

public record AutoReleaseRequest(int? TimeMinutes, int? MaxAttempts);
