namespace UMBRAL_Back_end.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using UMBRAL_Back_end.Application.Sessions.Commands.CreateSession;
using UMBRAL_Back_end.Application.Sessions.Queries.GetSessionById;
using UMBRAL_Back_end.Application.Sessions.Queries.GetSessions;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISender _sender;

    public SessionsController(ISender sender)
    {
        _sender = sender;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid? missionId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionsQuery(missionId, status), cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetSessionByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound(result.Error);
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
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value)
            : BadRequest(result.Error);
    }
}

// ── Request records ───────────────────────────────────────────────────────────

public record CreateSessionRequest(
    Guid MissionId,
    string Name,
    DateTime? ScheduledAt);
