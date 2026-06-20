namespace ClueService.Adapter.Controllers;

using ClueService.Domain.Common;
using ClueService.Domain.StageLookup;
using ClueService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// HU-27 — service-to-service endpoints for the global sync-health dashboard.
///
/// Reports the local <c>StagesLookup</c> projection state and exposes a manual
/// reproject that re-seeds it from StageService over HTTP.
/// </summary>
[ApiController]
[Route("api/internal/sync-health")]
public class InternalSyncHealthController : ControllerBase
{
    private readonly CluesDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<InternalSyncHealthController> _logger;

    public InternalSyncHealthController(
        CluesDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<InternalSyncHealthController> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try
        {
            var totalClues = await _context.Clues.CountAsync(ct);
            var stagesLookupCount = await _context.StagesLookup.CountAsync(ct);
            DateTime? stagesLookupMaxCreatedAt = await _context.StagesLookup.AnyAsync(ct)
                ? await _context.StagesLookup.MaxAsync(s => (DateTime?)s.CreatedAt, ct)
                : null;

            return Ok(new ClueServiceSyncHealthDto(
                TotalClues: totalClues,
                StagesLookupCount: stagesLookupCount,
                StagesLookupMaxCreatedAt: stagesLookupMaxCreatedAt));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Get), nameof(InternalSyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }

    [HttpPost("reproject")]
    public async Task<IActionResult> Reproject(CancellationToken ct)
    {
        try
        {
            var stageUrl = _configuration["StageServiceUrl"] ?? "http://localhost:5093/";
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri(stageUrl);

            var response = await client.GetAsync("api/internal/sync-health/stages-feed", ct);
            if (!response.IsSuccessStatusCode)
                return StatusCode(503, new { error = "StageService unreachable" });

            var json = await response.Content.ReadAsStringAsync(ct);
            var stages = System.Text.Json.JsonSerializer.Deserialize<List<UpstreamStageFeedItem>>(
                json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? [];

            var existing = await _context.StagesLookup.ToListAsync(ct);
            var existingById = existing.ToDictionary(s => s.Id);
            var seen = new HashSet<Guid>();

            foreach (var s in stages)
            {
                seen.Add(s.Id);
                if (!existingById.ContainsKey(s.Id))
                    _context.StagesLookup.Add(StageLookup.Create(s.Id, s.MissionId, s.Title));
            }

            foreach (var row in existing)
                if (!seen.Contains(row.Id))
                    _context.StagesLookup.Remove(row);

            var changes = await _context.SaveChangesAsync(ct);
            return Ok(new ClueReprojectResultDto(
                ProjectionId: "stage-lookup",
                UpstreamStages: stages.Count,
                ChangedRows: changes,
                CompletedAt: DateTime.UtcNow));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en {Action} de {Controller}.", nameof(Reproject), nameof(InternalSyncHealthController));
            return StatusCode(StatusCodes.Status500InternalServerError,
                new Error("ServerError", "Ha ocurrido un error inesperado. Intente nuevamente más tarde."));
        }
    }
}

public record ClueServiceSyncHealthDto(
    int TotalClues,
    int StagesLookupCount,
    DateTime? StagesLookupMaxCreatedAt);

public record UpstreamStageFeedItem(Guid Id, Guid MissionId, string Title);

public record ClueReprojectResultDto(
    string ProjectionId,
    int UpstreamStages,
    int ChangedRows,
    DateTime CompletedAt);
