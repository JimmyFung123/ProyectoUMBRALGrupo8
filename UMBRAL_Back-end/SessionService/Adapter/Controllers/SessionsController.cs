namespace SessionService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.Sessions.Commands.CancelSession;
using SessionService.Application.Sessions.Commands.CreateSession;
using SessionService.Application.Sessions.Commands.UpdateSession;
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
}

public record CreateSessionRequest(Guid MissionId, string Name, DateTime? ScheduledAt);
public record UpdateSessionRequest(string Name, DateTime? ScheduledAt);
