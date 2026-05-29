namespace StageService.Adapter.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StageService.Infrastructure.Persistence;

/// <summary>
/// HU-27 — service-to-service endpoints used by SessionService to build the
/// global sync-health dashboard.
///
/// Exposes:
///   - Local source counts (total stages) so other services can detect drift in
///     their derived projections (StageCountLookup in MissionService,
///     StageLookup in ClueService).
///   - Local replica health for the <c>MissionsLookup</c> table StageService
///     keeps in sync via RabbitMQ.
///   - A manual reproject action that re-fetches missions over HTTP and re-seeds
///     the <c>MissionsLookup</c> table when an admin detects drift on the
///     dashboard.
/// </summary>
[ApiController]
[Route("api/internal/sync-health")]
public class InternalSyncHealthController : ControllerBase
{
    private readonly StagesDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public InternalSyncHealthController(
        StagesDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    /// <summary>
    /// Reports the source count owned by this service plus the health of the
    /// local <c>MissionsLookup</c> projection.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var totalStages = await _context.Stages.CountAsync(ct);

        var missionsLookupCount = await _context.MissionsLookup.CountAsync(ct);
        DateTime? missionsLookupMaxUpdatedAt = await _context.MissionsLookup.AnyAsync(ct)
            ? await _context.MissionsLookup.MaxAsync(m => (DateTime?)m.LastUpdatedAt, ct)
            : null;

        return Ok(new StageServiceSyncHealthDto(
            TotalStages: totalStages,
            MissionsLookupCount: missionsLookupCount,
            MissionsLookupMaxUpdatedAt: missionsLookupMaxUpdatedAt));
    }

    /// <summary>
    /// Lightweight feed of every stage (id + missionId) — used by
    /// MissionService's StageCountLookup reproject and ClueService's StageLookup
    /// reproject. Not for end users.
    /// </summary>
    [HttpGet("stages-feed")]
    public async Task<IActionResult> StagesFeed(CancellationToken ct)
    {
        var stages = await _context.Stages
            .AsNoTracking()
            .Select(s => new StageFeedItemDto(s.Id, s.MissionId, s.Title))
            .ToListAsync(ct);
        return Ok(stages);
    }

    /// <summary>
    /// Re-seeds the local <c>MissionsLookup</c> table from MissionService over
    /// HTTP. Existing rows are refreshed in place, new ones are inserted, and
    /// rows for missions that no longer exist upstream are removed.
    /// </summary>
    [HttpPost("reproject")]
    public async Task<IActionResult> Reproject(CancellationToken ct)
    {
        var missionUrl = _configuration["MissionServiceUrl"] ?? "http://localhost:5091/";
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(missionUrl);

        var response = await client.GetAsync("api/missions", ct);
        if (!response.IsSuccessStatusCode)
            return StatusCode(503, new { error = "MissionService unreachable" });

        var json = await response.Content.ReadAsStringAsync(ct);
        var missions = System.Text.Json.JsonSerializer.Deserialize<List<UpstreamMissionDto>>(
            json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? [];

        var existing = await _context.MissionsLookup.ToListAsync(ct);
        var existingById = existing.ToDictionary(m => m.Id);
        var seen = new HashSet<Guid>();

        foreach (var m in missions)
        {
            seen.Add(m.Id);
            if (existingById.TryGetValue(m.Id, out var row))
            {
                row.UpdateStatus(m.Status);
            }
            else
            {
                _context.MissionsLookup.Add(
                    StageService.Domain.MissionLookup.MissionLookup.Create(m.Id, m.Name, m.Status));
            }
        }

        // Drop rows for missions that no longer exist upstream.
        foreach (var row in existing)
            if (!seen.Contains(row.Id))
                _context.MissionsLookup.Remove(row);

        var changes = await _context.SaveChangesAsync(ct);

        return Ok(new ReprojectResultDto(
            ProjectionId: "missions-lookup-stage",
            UpstreamCount: missions.Count,
            ChangedRows: changes,
            CompletedAt: DateTime.UtcNow));
    }
}

public record StageServiceSyncHealthDto(
    int TotalStages,
    int MissionsLookupCount,
    DateTime? MissionsLookupMaxUpdatedAt);

public record StageFeedItemDto(Guid Id, Guid MissionId, string Title);

public record UpstreamMissionDto(Guid Id, string Name, string Status);

public record ReprojectResultDto(
    string ProjectionId,
    int UpstreamCount,
    int ChangedRows,
    DateTime CompletedAt);
