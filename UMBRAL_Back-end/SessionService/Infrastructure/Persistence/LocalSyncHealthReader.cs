namespace SessionService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SessionService.Application.SyncHealth;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;

/// <summary>
/// HU-27 — EF-backed implementation of <see cref="ILocalSyncHealthReader"/>.
/// Reads SessionService's own database for the two cards it owns
/// (MissionsLookup + StageCompletionRecords) and runs the two local
/// reproject/reconcile actions in a single transaction.
/// </summary>
public class LocalSyncHealthReader : ILocalSyncHealthReader
{
    private readonly SessionsDbContext _context;

    public LocalSyncHealthReader(SessionsDbContext context) => _context = context;

    public async Task<LocalSessionsSnapshot> ReadAsync(CancellationToken ct)
    {
        var missionsLookupCount = await _context.MissionsLookup.AsNoTracking().CountAsync(ct);
        DateTime? maxUpdated = await _context.MissionsLookup.AnyAsync(ct)
            ? await _context.MissionsLookup.MaxAsync(m => (DateTime?)m.LastUpdatedAt, ct)
            : null;

        var totalRecords = await _context.StageCompletionRecords.AsNoTracking().CountAsync(ct);

        // Flag drift = rows whose parent session is Completed (the finalized
        // state in the SessionStatus enum) but IncludedInStatistics is still
        // false — those should be visible to the dashboard but aren't yet.
        var completedSessionIds = await _context.Sessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Completed)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var completedSet = completedSessionIds.ToHashSet();

        var allRecords = await _context.StageCompletionRecords
            .AsNoTracking()
            .Select(r => new { r.SessionId, r.IncludedInStatistics })
            .ToListAsync(ct);

        var drift = allRecords.Count(r =>
            completedSet.Contains(r.SessionId) != r.IncludedInStatistics);

        // Pull session status for every session the team-service might
        // mention — used to label the ranking-projection cards.
        var sessionStatuses = await _context.Sessions
            .AsNoTracking()
            .Select(s => new { s.Id, s.Status })
            .ToListAsync(ct);

        var statusById = sessionStatuses.ToDictionary(s => s.Id, s => s.Status.ToString());

        return new LocalSessionsSnapshot(
            MissionsLookupCount: missionsLookupCount,
            MissionsLookupMaxUpdatedAt: maxUpdated,
            StageCompletionRecordsTotal: totalRecords,
            StageCompletionRecordsFlagDrift: drift,
            SessionStatusById: statusById);
    }

    public async Task<int> ReconcileStageCompletionRecordsAsync(CancellationToken ct)
    {
        var completedIds = await _context.Sessions
            .Where(s => s.Status == SessionStatus.Completed)
            .Select(s => s.Id)
            .ToListAsync(ct);
        var completedSet = completedIds.ToHashSet();

        var records = await _context.StageCompletionRecords.ToListAsync(ct);
        var fixedCount = 0;
        foreach (var r in records)
        {
            var shouldBeIncluded = completedSet.Contains(r.SessionId);
            if (shouldBeIncluded && !r.IncludedInStatistics)
            {
                r.IncludeInStatistics();
                fixedCount++;
            }
            // Records flagged true while their session isn't completed are not
            // un-flipped: the analytics fact table is append-only by design.
            // Surfacing them via the count drift is enough for HU-27.
        }

        if (fixedCount > 0)
            await _context.SaveChangesAsync(ct);
        return fixedCount;
    }

    public async Task<int> ReprojectMissionsLookupAsync(
        Func<CancellationToken, Task<IReadOnlyList<UpstreamMissionRow>>> sourceFetcher,
        CancellationToken ct)
    {
        var upstream = await sourceFetcher(ct);
        if (upstream.Count == 0) return 0; // safety net — see handler comment.

        var existing = await _context.MissionsLookup.ToListAsync(ct);
        var byId = existing.ToDictionary(m => m.Id);
        var seen = new HashSet<Guid>();
        var changes = 0;

        foreach (var row in upstream)
        {
            seen.Add(row.Id);
            if (byId.TryGetValue(row.Id, out var local))
            {
                if (local.Status != row.Status)
                {
                    local.UpdateStatus(row.Status);
                    changes++;
                }
            }
            else
            {
                _context.MissionsLookup.Add(MissionLookup.Create(row.Id, row.Name, row.Status));
                changes++;
            }
        }

        foreach (var local in existing)
        {
            if (!seen.Contains(local.Id))
            {
                _context.MissionsLookup.Remove(local);
                changes++;
            }
        }

        if (changes > 0)
            await _context.SaveChangesAsync(ct);
        return changes;
    }
}
