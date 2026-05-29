namespace SessionService.Application.SyncHealth;

// HU-27 — DTOs that mirror what each downstream service exposes through its
// /api/internal/sync-health endpoint. These are the transport contracts used
// by the HTTP clients; the aggregator handler turns them into the public
// ProjectionHealthDto list.

public record MissionServiceSyncSnapshot(
    int TotalMissions,
    int StageCountLookupRows,
    int StageCountLookupSum);

public record StageServiceSyncSnapshot(
    int TotalStages,
    int MissionsLookupCount,
    DateTime? MissionsLookupMaxUpdatedAt);

public record ClueServiceSyncSnapshot(
    int TotalClues,
    int StagesLookupCount,
    DateTime? StagesLookupMaxCreatedAt);

public record TeamServiceSyncSnapshot(
    int TotalTeams,
    int TotalProjections,
    IReadOnlyList<TeamServiceSyncSessionRow> Sessions);

public record TeamServiceSyncSessionRow(
    Guid SessionId,
    int TeamCount,
    int ProjectionCount,
    DateTime? LastUpdatedAt);

/// <summary>
/// Data pulled from the SessionService's own database to compose its two cards
/// (MissionsLookup-session and StageCompletionRecords).
/// </summary>
public record LocalSessionsSnapshot(
    int MissionsLookupCount,
    DateTime? MissionsLookupMaxUpdatedAt,
    int StageCompletionRecordsTotal,
    int StageCompletionRecordsFlagDrift,
    IReadOnlyDictionary<Guid, string> SessionStatusById);
