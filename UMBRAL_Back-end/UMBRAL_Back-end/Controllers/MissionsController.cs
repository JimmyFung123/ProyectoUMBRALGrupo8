namespace UMBRAL_Back_end.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using UMBRAL_Back_end.Application.Missions.Commands.ChangeMissionStatus;
using UMBRAL_Back_end.Application.Missions.Commands.CreateMission;
using UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;
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
}

public record CreateMissionRequest(string Name, string Description, DifficultyLevel Difficulty, int MaxDuration);
public record UpdateMissionRequest(string Name, string Description, DifficultyLevel Difficulty, int MaxDuration);
public record ChangeStatusRequest(bool Activate);
