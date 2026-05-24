namespace ClueService.Adapter.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ClueService.Application.Clues.Commands.AddClue;
using ClueService.Application.Clues.Commands.RemoveClue;
using ClueService.Application.Clues.Queries.GetCluesByStage;

[ApiController]
[Route("api/[controller]")]
public class CluesController : ControllerBase
{
    private readonly ISender _sender;
    public CluesController(ISender sender) => _sender = sender;

    [HttpGet]
    public async Task<IActionResult> GetByStage([FromQuery] Guid stageId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCluesByStageQuery(stageId), cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddClueRequest request, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new AddClueCommand(request.StageId, request.Content, request.AutoReleaseAfterMinutes), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RemoveClueCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }
}

public record AddClueRequest(Guid StageId, string Content, int? AutoReleaseAfterMinutes = null);
