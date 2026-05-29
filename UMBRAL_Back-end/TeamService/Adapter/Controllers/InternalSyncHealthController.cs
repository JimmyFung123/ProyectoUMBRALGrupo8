namespace TeamService.Adapter.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamService.Application.Rankings;
using TeamService.Infrastructure.Persistence;

/// <summary>
/// HU-27 — service-to-service endpoints for the global sync-health dashboard.
///
/// Reports per-session counts of the write model (<c>Teams</c>) versus the
/// CQRS read projection (<c>RankingProjections</c>) plus the most recent
/// projector run timestamp. The dashboard uses this to flag sessions whose
/// projection has drifted from the write model and to expose a per-session
/// reproject action driven by <see cref="IRankingProjector"/>.
/// </summary>
[ApiController]
[Route("api/internal/sync-health")]
public class InternalSyncHealthController : ControllerBase
{
    private readonly TeamsDbContext _context;
    private readonly IRankingProjector _projector;

    public InternalSyncHealthController(TeamsDbContext context, IRankingProjector projector)
    {
        _context = context;
        _projector = projector;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var totalTeams = await _context.Teams.CountAsync(ct);
        var totalProjections = await _context.RankingProjections.CountAsync(ct);

        // Per-session breakdown: every session that has at least one team OR at
        // least one projection row, so the dashboard can surface sessions whose
        // projection is leftover after a write-model wipe (and vice versa).
        var teamCounts = await _context.Teams
            .GroupBy(t => t.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var projectionData = await _context.RankingProjections
            .GroupBy(p => p.SessionId)
            .Select(g => new
            {
                SessionId = g.Key,
                Count = g.Count(),
                MaxUpdatedAt = g.Max(p => p.UpdatedAt)
            })
            .ToListAsync(ct);

        var sessionIds = teamCounts.Select(t => t.SessionId)
            .Union(projectionData.Select(p => p.SessionId))
            .ToList();

        var teamsBySession = teamCounts.ToDictionary(t => t.SessionId, t => t.Count);
        var projectionsBySession = projectionData.ToDictionary(p => p.SessionId);

        var sessions = sessionIds
            .Select(sid =>
            {
                teamsBySession.TryGetValue(sid, out var teamCount);
                projectionsBySession.TryGetValue(sid, out var proj);
                return new RankingHealthSessionDto(
                    SessionId: sid,
                    TeamCount: teamCount,
                    ProjectionCount: proj?.Count ?? 0,
                    LastUpdatedAt: proj?.MaxUpdatedAt);
            })
            .OrderByDescending(s => s.LastUpdatedAt ?? DateTime.MinValue)
            .ToList();

        return Ok(new TeamServiceSyncHealthDto(
            TotalTeams: totalTeams,
            TotalProjections: totalProjections,
            Sessions: sessions));
    }

    /// <summary>
    /// Forces a full rebuild of the ranking projection for a single session.
    /// Internally delegates to <see cref="IRankingProjector.RebuildAsync"/>,
    /// which is the same code path command handlers use after every write.
    /// </summary>
    [HttpPost("ranking/{sessionId:guid}/reproject")]
    public async Task<IActionResult> ReprojectSessionRanking(Guid sessionId, CancellationToken ct)
    {
        await _projector.RebuildAsync(sessionId, ct);
        await _context.SaveChangesAsync(ct);

        var teamCount = await _context.Teams.CountAsync(t => t.SessionId == sessionId, ct);
        var projectionCount = await _context.RankingProjections.CountAsync(p => p.SessionId == sessionId, ct);

        return Ok(new RankingReprojectResultDto(
            ProjectionId: "ranking-projection",
            SessionId: sessionId,
            TeamCount: teamCount,
            ProjectionCount: projectionCount,
            CompletedAt: DateTime.UtcNow));
    }
}

public record RankingHealthSessionDto(
    Guid SessionId,
    int TeamCount,
    int ProjectionCount,
    DateTime? LastUpdatedAt);

public record TeamServiceSyncHealthDto(
    int TotalTeams,
    int TotalProjections,
    IReadOnlyList<RankingHealthSessionDto> Sessions);

public record RankingReprojectResultDto(
    string ProjectionId,
    Guid SessionId,
    int TeamCount,
    int ProjectionCount,
    DateTime CompletedAt);
