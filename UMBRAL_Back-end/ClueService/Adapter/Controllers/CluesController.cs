namespace ClueService.Adapter.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ClueService.Adapter.Extensions;
using ClueService.Application.Clues.Commands.AddClue;
using ClueService.Application.Clues.Commands.RemoveClue;
using ClueService.Application.Clues.Commands.UpdateClue;
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
        var result = await _sender.Send(
            new AddClueCommand(
                request.StageId,
                request.Order ?? 0,
                request.Content,
                request.Latitude,
                request.Longitude,
                request.RadiusMeters,
                request.AutoReleaseAfterMinutes),
            cancellationToken);

        return result.ToHttpResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateClueRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new UpdateClueCommand(
                id,
                request.Order,
                request.Content,
                request.Latitude,
                request.Longitude,
                request.RadiusMeters),
            cancellationToken);

        return result.ToNoContentResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RemoveClueCommand(id), cancellationToken);
        return result.ToNoContentResult();
    }
}

public record AddClueRequest(
    Guid StageId,
    int? Order = null,
    string? Content = null,
    double? Latitude = null,
    double? Longitude = null,
    int? RadiusMeters = null,
    int? AutoReleaseAfterMinutes = null);

public record UpdateClueRequest(
    int Order,
    string? Content = null,
    double? Latitude = null,
    double? Longitude = null,
    int? RadiusMeters = null);
