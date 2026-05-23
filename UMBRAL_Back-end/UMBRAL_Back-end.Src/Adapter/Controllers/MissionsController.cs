namespace UMBRAL_Back_end.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using UMBRAL_Back_end.Application.Missions.Commands.AddStageToMission;
using UMBRAL_Back_end.Application.Missions.Commands.ChangeMissionStatus;
using UMBRAL_Back_end.Application.Missions.Commands.CreateMission;
using UMBRAL_Back_end.Application.Missions.Commands.RemoveStage;
using UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;
using UMBRAL_Back_end.Application.Missions.Commands.UpdateStage;
using UMBRAL_Back_end.Application.Missions.Queries.GetMissionById;
using UMBRAL_Back_end.Application.Missions.Queries.GetMissions;
using UMBRAL_Back_end.Domain.Missions;

[ApiController]
[Route("api/[controller]")]
public class MissionsController : ControllerBase
{
    private readonly ISender _sender;

    public MissionsController(ISender sender)
    {
        _sender = sender;
    }

    // ── Missions ─────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMissionsQuery(), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMissionByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMissionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new CreateMissionCommand(request.Name, request.Description, request.Difficulty, request.MaxDuration),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : Conflict(result.Error);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMissionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateMissionCommand(id, request.Name, request.Description, request.Difficulty, request.MaxDuration),
            cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new ChangeMissionStatusCommand(id, request.Activate),
            cancellationToken);

        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    // ── Stages ───────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/stages")]
    public async Task<IActionResult> AddStage(Guid id, [FromBody] AddStageRequest request, CancellationToken cancellationToken)
    {
        var command = new AddStageToMissionCommand(
            MissionId: id,
            Title: request.Title,
            Order: request.Order,
            Type: request.Type,
            BaseScore: request.BaseScore,
            Question: request.Question,
            Options: request.Options?.Select(o => new TriviaOptionInput(o.Text, o.IsCorrect)).ToList(),
            Latitude: request.Latitude,
            Longitude: request.Longitude,
            QrCode: request.QrCode);

        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPut("{missionId:guid}/stages/{stageId:guid}")]
    public async Task<IActionResult> UpdateStage(
        Guid missionId, Guid stageId,
        [FromBody] UpdateStageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateStageCommand(
            MissionId: missionId,
            StageId: stageId,
            Title: request.Title,
            Order: request.Order,
            BaseScore: request.BaseScore,
            Question: request.Question,
            Options: request.Options?.Select(o => new TriviaOptionInput(o.Text, o.IsCorrect)).ToList(),
            Latitude: request.Latitude,
            Longitude: request.Longitude,
            QrCode: request.QrCode);

        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }

    [HttpDelete("{missionId:guid}/stages/{stageId:guid}")]
    public async Task<IActionResult> RemoveStage(
        Guid missionId, Guid stageId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RemoveStageCommand(missionId, stageId), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(result.Error);
    }
}

// ── Request records ───────────────────────────────────────────────────────────

public record CreateMissionRequest(string Name, string Description, DifficultyLevel Difficulty, int MaxDuration);
public record UpdateMissionRequest(string Name, string Description, DifficultyLevel Difficulty, int MaxDuration);
public record ChangeStatusRequest(bool Activate);

public record AddStageRequest(
    string Title,
    int Order,
    StageType Type,
    int BaseScore,
    string? Question,
    List<OptionRequest>? Options,
    double? Latitude,
    double? Longitude,
    string? QrCode);

public record UpdateStageRequest(
    string Title,
    int Order,
    int BaseScore,
    string? Question,
    List<OptionRequest>? Options,
    double? Latitude,
    double? Longitude,
    string? QrCode);

public record OptionRequest(string Text, bool IsCorrect);
