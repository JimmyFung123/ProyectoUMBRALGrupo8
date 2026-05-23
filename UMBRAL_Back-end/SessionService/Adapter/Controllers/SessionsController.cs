namespace SessionService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.Sessions.Commands.CreateSession;
using SessionService.Application.Sessions.Queries.GetSessions;

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
}

public record CreateSessionRequest(Guid MissionId, string Name, DateTime? ScheduledAt);
