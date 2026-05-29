namespace SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

/// <summary>
/// HU-27 — payload returned by every reproject/reconcile command so the
/// dashboard can confirm the action ran and surface how many rows changed.
/// </summary>
public record ReprojectActionResultDto(
    string ProjectionId,
    bool Success,
    int ChangedRows,
    string Detail,
    DateTime CompletedAt);
