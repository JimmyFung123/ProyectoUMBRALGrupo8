namespace StageService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using StageService.Application.Stages.Commands.AddStage;
using StageService.Application.Stages.Commands.RemoveStage;
using StageService.Application.Stages.Commands.SetAutoRelease;
using StageService.Application.Stages.Commands.UpdateStage;
using StageService.Application.Stages.Queries.GetStageById;
using StageService.Application.Stages.Queries.GetStagesByMission;

[ApiController]
[Route("api/[controller]")]
public class StagesController : ControllerBase
{
    private readonly ISender _sender;
    public StagesController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetByMission([FromQuery] Guid missionId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetStagesByMissionQuery(missionId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns a single stage with all options (including IsCorrect). Called by SessionService only.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetStageByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddStageRequest request, CancellationToken cancellationToken)
    {
        var options = request.Options?.Select(o => (o.Text, o.IsCorrect)).ToList();
        var command = new AddStageCommand(
            request.MissionId, request.Title, request.Type, request.Order, request.BaseScore,
            request.Question, options, request.Latitude, request.Longitude, request.QrCode);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStageRequest request, CancellationToken cancellationToken)
    {
        var options = request.Options?.Select(o => (o.Text, o.IsCorrect)).ToList();
        var command = new UpdateStageCommand(
            id, request.Title, request.Order, request.BaseScore,
            request.Question, options, request.Latitude, request.Longitude, request.QrCode,
            request.AutoReleaseTimeMinutes, request.AutoReleaseMaxAttempts);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, [FromQuery] Guid missionId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RemoveStageCommand(id, missionId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpPatch("{id:guid}/auto-release")]
    public async Task<IActionResult> SetAutoRelease(Guid id, [FromBody] AutoReleaseRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new SetAutoReleaseCommand(id, request.TimeMinutes, request.MaxAttempts), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
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
