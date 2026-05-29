namespace SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

using MediatR;
using SessionService.Application.SyncHealth;

/// <summary>
/// HU-27 — rebuilds the local <c>MissionsLookup</c> table from MissionService.
/// Returns a non-success result when MissionService is unreachable so the
/// admin sees a clear failure on the dashboard instead of the table getting
/// wiped silently.
/// </summary>
public class ReprojectSessionMissionsLookupCommandHandler
    : IRequestHandler<ReprojectSessionMissionsLookupCommand, ReprojectActionResultDto>
{
    private readonly IMissionServiceSyncClient _missionClient;
    private readonly ILocalSyncHealthReader _localReader;

    public ReprojectSessionMissionsLookupCommandHandler(
        IMissionServiceSyncClient missionClient,
        ILocalSyncHealthReader localReader)
    {
        _missionClient = missionClient;
        _localReader = localReader;
    }

    public async Task<ReprojectActionResultDto> Handle(
        ReprojectSessionMissionsLookupCommand request,
        CancellationToken ct)
    {
        // Validate upstream is reachable BEFORE wiping the local table. The
        // snapshot endpoint is cheap and the right signal — if it errors we
        // bail out before deleting any rows.
        var snapshot = await _missionClient.GetSnapshotAsync(ct);
        if (snapshot is null)
        {
            return new ReprojectActionResultDto(
                ProjectionId: "missions-lookup-session",
                Success: false,
                ChangedRows: 0,
                Detail: "MissionService no responde — no se rebuilt la réplica para no perder datos.",
                CompletedAt: DateTime.UtcNow);
        }

        var changed = await _localReader.ReprojectMissionsLookupAsync(
            sourceFetcher: ctk => _missionClient.GetMissionsAsync(ctk),
            ct);

        return new ReprojectActionResultDto(
            ProjectionId: "missions-lookup-session",
            Success: true,
            ChangedRows: changed,
            Detail: $"{changed} fila(s) afectada(s) en SessionService.MissionsLookup.",
            CompletedAt: DateTime.UtcNow);
    }
}
