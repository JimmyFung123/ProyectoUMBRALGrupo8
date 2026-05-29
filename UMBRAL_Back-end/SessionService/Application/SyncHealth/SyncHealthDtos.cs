namespace SessionService.Application.SyncHealth;

/// <summary>
/// HU-27 — single projection card shown by the admin sync-health dashboard.
///
/// Combines the write-side count (source of truth), the read-side count (the
/// CQRS projection) and the freshness signal (<see cref="LastUpdatedAt"/> +
/// <see cref="LagSeconds"/>) into a status that drives the badge color in the UI.
/// </summary>
public record ProjectionHealthDto(
    string ProjectionId,
    string DisplayName,
    string OwningService,
    string SourceModel,
    string ReadModel,
    int SourceCount,
    int ReadCount,
    DateTime? LastUpdatedAt,
    int? LagSeconds,
    string Status,
    string Detail,
    bool SupportsReproject,
    bool RequiresSessionId,
    IReadOnlyList<RankingProjectionSessionDto>? Sessions);

/// <summary>
/// Per-session row used only by the RankingProjection card. Each row carries
/// the data needed for the admin to pick a specific session from the dropdown
/// and trigger a focused reproject.
/// </summary>
public record RankingProjectionSessionDto(
    Guid SessionId,
    string SessionStatus,
    int TeamCount,
    int ProjectionCount,
    DateTime? LastUpdatedAt,
    int? LagSeconds,
    string Status);

/// <summary>Full response payload for <c>GET /api/sync-health</c>.</summary>
public record SyncHealthDto(
    DateTime GeneratedAt,
    IReadOnlyList<ProjectionHealthDto> Projections);
