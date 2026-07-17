namespace ClueService.Adapter.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using ClueService.Adapter.Extensions;
using ClueService.Application.Clues.Commands.AddClue;
using ClueService.Application.Clues.Commands.RemoveClue;
using ClueService.Application.Clues.Commands.UpdateClue;
using ClueService.Application.Clues.Queries.GetCluesByStage;
using ClueService.Domain.Common;

[ApiController]
[Route("api/[controller]")]
public class CluesController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<CluesController> _logger;

    public CluesController(ISender sender, ILogger<CluesController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetByStage([FromQuery] Guid stageId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new GetCluesByStageQuery(stageId), cancellationToken);
            return Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(GetByStage), nameof(CluesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddClueRequest request, CancellationToken cancellationToken)
    {
        try
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Add), nameof(CluesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateClueRequest request,
        CancellationToken cancellationToken)
    {
        try
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Update), nameof(CluesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _sender.Send(new RemoveClueCommand(id), cancellationToken);
            return result.ToNoContentResult();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Remove), nameof(CluesController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }
}
