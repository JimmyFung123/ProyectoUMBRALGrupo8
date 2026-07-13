namespace TeamService.Infrastructure.Projections;

using Microsoft.EntityFrameworkCore;
using TeamService.Application.Rankings;
using TeamService.Domain.Rankings;
using TeamService.Domain.Teams;
using TeamService.Infrastructure.Persistence;

/// <summary>
/// EF-backed implementation of <see cref="IRankingProjector"/> (HU-24).
///
/// Recomputes the full session ranking from the <c>Team</c> aggregate and
/// upserts the rows of <c>RankingProjections</c>. Shares the calling handler's
/// <see cref="TeamsDbContext"/>, so the projector's changes commit in the same
/// <c>SaveChanges</c> as the write that triggered it — the projection cannot
/// drift from the write model within a single request.
///
/// Reading <c>_context.Teams</c> after the handler mutated an entity returns
/// the in-memory tracked instance (with its new <c>Score</c> /
/// <c>LastStageCompletedAt</c>) — EF Core's change tracker reconciles the
/// query result with pending changes, so the projection sees the latest state.
///
/// The RB-08 sort + dense-rank algorithm itself lives in
/// <see cref="TeamRankingCalculator"/> (Domain Service, pure, no I/O) — this
/// class only handles reading teams / upserting projection rows.
/// </summary>
public class RankingProjector : IRankingProjector
{
    private readonly TeamsDbContext _context;

    public RankingProjector(TeamsDbContext context) => _context = context;

    public async Task RebuildAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        // 1. Latest team state. Read from DB first (modifications on tracked
        //    entities are reconciled automatically), then merge in entities that
        //    are tracked locally as `Added` — those are not yet in the DB so the
        //    SQL query above can't see them (relevant for CreateTeam). Exclude
        //    entities tracked as `Deleted` — the SQL query still returns their row
        //    until SaveChanges runs, but they must not reappear in the ranking
        //    (relevant for LeaveTeam, which deletes the team in the same unit of
        //    work as this rebuild).
        var teams = (await _context.Teams
            .Where(t => t.SessionId == sessionId)
            .ToListAsync(cancellationToken))
            .Where(t => _context.Entry(t).State != EntityState.Deleted)
            .ToList();

        foreach (var localTeam in _context.Teams.Local.Where(t => t.SessionId == sessionId))
        {
            if (_context.Entry(localTeam).State != EntityState.Deleted && !teams.Contains(localTeam))
                teams.Add(localTeam);
        }

        // 2. Existing projection rows (tracked, so we can refresh in place).
        var existing = await _context.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .ToListAsync(cancellationToken);
        var existingByTeam = existing.ToDictionary(p => p.TeamId);

        // 3-4. RB-08: orden + ranking denso (Domain Service, lógica pura sin I/O).
        var ranked = TeamRankingCalculator.Calculate(teams);

        var now = DateTime.UtcNow;
        var seenTeamIds = new HashSet<Guid>(ranked.Count);

        // Upsert each team's projection with the computed Rank and Position.
        foreach (var r in ranked)
        {
            var team = r.Team;
            seenTeamIds.Add(team.Id);

            if (existingByTeam.TryGetValue(team.Id, out var row))
            {
                row.Refresh(
                    teamName: team.Name,
                    score: team.Score,
                    rank: r.Rank,
                    position: r.Position,
                    currentStageOrder: team.CurrentStageOrder,
                    isConnected: team.IsConnected,
                    lastStageCompletedAt: team.LastStageCompletedAt,
                    updatedAt: now);
            }
            else
            {
                var newRow = RankingProjection.Create(
                    sessionId: sessionId,
                    teamId: team.Id,
                    teamName: team.Name,
                    score: team.Score,
                    rank: r.Rank,
                    position: r.Position,
                    currentStageOrder: team.CurrentStageOrder,
                    isConnected: team.IsConnected,
                    lastStageCompletedAt: team.LastStageCompletedAt,
                    updatedAt: now);
                await _context.RankingProjections.AddAsync(newRow, cancellationToken);
            }
        }

        // 5. Drop stale rows (teams that were removed from the session — defensive,
        //    today only triggered by session cancellation which uses RemoveSessionAsync).
        foreach (var row in existing)
        {
            if (!seenTeamIds.Contains(row.TeamId))
                _context.RankingProjections.Remove(row);
        }
    }

    public async Task RemoveSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var rows = await _context.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        _context.RankingProjections.RemoveRange(rows);
    }
}
