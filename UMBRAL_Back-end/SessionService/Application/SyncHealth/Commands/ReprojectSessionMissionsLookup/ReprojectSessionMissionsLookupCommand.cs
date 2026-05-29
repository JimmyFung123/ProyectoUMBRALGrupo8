namespace SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

using MediatR;

/// <summary>
/// HU-27 — manual reproject of SessionService's MissionsLookup replica.
/// Pulls the authoritative list of missions from MissionService over HTTP and
/// reseeds the local table when an admin detects drift on the dashboard.
/// </summary>
public record ReprojectSessionMissionsLookupCommand() : IRequest<ReprojectActionResultDto>;
