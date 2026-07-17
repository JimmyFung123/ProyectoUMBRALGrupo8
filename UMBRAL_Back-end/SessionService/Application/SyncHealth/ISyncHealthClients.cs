namespace SessionService.Application.SyncHealth;

/// <summary>
/// HU-27 — Application-layer ports for the four downstream services that
/// participate in the global sync-health dashboard. Each method returns
/// <c>null</c> when the underlying service is unreachable so the aggregator
/// can still render the remaining cards instead of failing the whole request.
/// </summary>
public interface IMissionServiceSyncClient
{
    Task<MissionServiceSyncSnapshot?> GetSnapshotAsync(CancellationToken ct);
    Task<bool> ReprojectStageCountLookupAsync(CancellationToken ct);

    /// <summary>
    /// Lightweight feed of every mission (id + name + status). Used by
    /// SessionService's local <c>MissionsLookup</c> reproject so the handler can
    /// re-seed its replica without StageService in the loop.
    /// </summary>
    Task<IReadOnlyList<UpstreamMissionRow>> GetMissionsAsync(CancellationToken ct);
}

public interface IStageServiceSyncClient
{
    Task<StageServiceSyncSnapshot?> GetSnapshotAsync(CancellationToken ct);
    Task<bool> ReprojectMissionsLookupAsync(CancellationToken ct);
}

public interface IClueServiceSyncClient
{
    Task<ClueServiceSyncSnapshot?> GetSnapshotAsync(CancellationToken ct);
    Task<bool> ReprojectStageLookupAsync(CancellationToken ct);
}

public interface ITeamServiceSyncClient
{
    Task<TeamServiceSyncSnapshot?> GetSnapshotAsync(CancellationToken ct);
    Task<bool> ReprojectRankingForSessionAsync(Guid sessionId, CancellationToken ct);
}

/// <summary>
/// Reader for the SessionService's own database so the aggregator handler can
/// be unit-tested without spinning up EF Core. Implementation in Infrastructure
/// queries <c>SessionsDbContext</c> directly.
/// </summary>
public interface ILocalSyncHealthReader
{
    Task<LocalSessionsSnapshot> ReadAsync(CancellationToken ct);
    Task<int> ReconcileStageCompletionRecordsAsync(CancellationToken ct);
    Task<int> ReprojectMissionsLookupAsync(Func<CancellationToken, Task<IReadOnlyList<UpstreamMissionRow>>> sourceFetcher, CancellationToken ct);
}

/// <summary>Row returned by MissionService when re-seeding the local MissionsLookup.</summary>
public record UpstreamMissionRow(Guid Id, string Name, string Status);
