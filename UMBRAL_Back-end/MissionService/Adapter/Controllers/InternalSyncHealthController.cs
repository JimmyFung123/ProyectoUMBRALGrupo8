namespace UMBRAL_Back_end.Adapter.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Infrastructure.Persistence;

/// <summary>
/// HU-27 — service-to-service endpoints for the global sync-health dashboard.
///
/// Reports:
///   - The total mission count (source of truth) so the dashboard can detect
///     drift on every <c>MissionsLookup</c> replica (SessionService + StageService).
///   - The health of the local <c>StageCountLookup</c> projection (sum of counts
///     vs total stages reported by StageService).
///
/// Manual reproject re-builds <c>StageCountLookup</c> from StageService's stages
/// feed so an admin can fix drift without restarting the service.
/// </summary>
[ApiController]
[Route("api/internal/sync-health")]
public class InternalSyncHealthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public InternalSyncHealthController(
        AppDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var totalMissions = await _context.Missions.CountAsync(ct);

        var stageCountLookupRows = await _context.StageCountLookup.CountAsync(ct);
        var stageCountLookupSum = await _context.StageCountLookup.AnyAsync(ct)
            ? await _context.StageCountLookup.SumAsync(s => s.Count, ct)
            : 0;

        return Ok(new MissionServiceSyncHealthDto(
            TotalMissions: totalMissions,
            StageCountLookupRows: stageCountLookupRows,
            StageCountLookupSum: stageCountLookupSum));
    }

    [HttpPost("reproject")]
    public async Task<IActionResult> Reproject(CancellationToken ct)
    {
        var stageUrl = _configuration["StageServiceUrl"] ?? "http://localhost:5093/";
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(stageUrl);

        var response = await client.GetAsync("api/internal/sync-health/stages-feed", ct);
        if (!response.IsSuccessStatusCode)
            return StatusCode(503, new { error = "StageService unreachable" });

        var json = await response.Content.ReadAsStringAsync(ct);
        var stages = System.Text.Json.JsonSerializer.Deserialize<List<UpstreamStageDto>>(
            json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];

        // Group stages by mission and rebuild the lookup from scratch.
        var countsByMission = stages
            .GroupBy(s => s.MissionId)
            .ToDictionary(g => g.Key, g => g.Count());

        var existing = await _context.StageCountLookup.ToListAsync(ct);

        // Remove rows that no longer have any stage upstream.
        foreach (var row in existing)
            if (!countsByMission.ContainsKey(row.MissionId))
                _context.StageCountLookup.Remove(row);

        // Re-insert/refresh rows so Count exactly matches upstream.
        var byMission = existing.ToDictionary(r => r.MissionId);
        foreach (var (missionId, count) in countsByMission)
        {
            if (byMission.TryGetValue(missionId, out var row))
            {
                // Cheap path: drop and re-insert when count differs (entity has
                // no setter — only Increment/Decrement — so we rebuild it).
                _context.StageCountLookup.Remove(row);
            }

            var fresh = StageCountLookup.Create(missionId);
            for (var i = 1; i < count; i++)
                fresh.Increment();
            _context.StageCountLookup.Add(fresh);
        }

        var changes = await _context.SaveChangesAsync(ct);
        return Ok(new MissionReprojectResultDto(
            ProjectionId: "stage-count-lookup",
            UpstreamStages: stages.Count,
            MissionsWithStages: countsByMission.Count,
            ChangedRows: changes,
            CompletedAt: DateTime.UtcNow));
    }
}

public record MissionServiceSyncHealthDto(
    int TotalMissions,
    int StageCountLookupRows,
    int StageCountLookupSum);

public record UpstreamStageDto(Guid Id, Guid MissionId, string Title);

public record MissionReprojectResultDto(
    string ProjectionId,
    int UpstreamStages,
    int MissionsWithStages,
    int ChangedRows,
    DateTime CompletedAt);
