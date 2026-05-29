namespace SessionService.Application.SyncHealth.Commands.ProxyReprojects;

using MediatR;
using SessionService.Application.SyncHealth;
using SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

// HU-27 — one handler per proxy command. Each one calls the corresponding
// service's /api/internal/sync-health endpoint via its sync client, then
// translates the boolean outcome into the dashboard's standard payload.

public class ReprojectStageMissionsLookupCommandHandler
    : IRequestHandler<ReprojectStageMissionsLookupCommand, ReprojectActionResultDto>
{
    private readonly IStageServiceSyncClient _client;
    public ReprojectStageMissionsLookupCommandHandler(IStageServiceSyncClient client) => _client = client;

    public async Task<ReprojectActionResultDto> Handle(ReprojectStageMissionsLookupCommand request, CancellationToken ct)
    {
        var ok = await _client.ReprojectMissionsLookupAsync(ct);
        return new ReprojectActionResultDto(
            ProjectionId: "missions-lookup-stage",
            Success: ok,
            ChangedRows: 0,
            Detail: ok
                ? "StageService rebuilt su réplica MissionsLookup desde MissionService."
                : "StageService rechazó el reproject o MissionService no respondió.",
            CompletedAt: DateTime.UtcNow);
    }
}

public class ReprojectStageCountLookupCommandHandler
    : IRequestHandler<ReprojectStageCountLookupCommand, ReprojectActionResultDto>
{
    private readonly IMissionServiceSyncClient _client;
    public ReprojectStageCountLookupCommandHandler(IMissionServiceSyncClient client) => _client = client;

    public async Task<ReprojectActionResultDto> Handle(ReprojectStageCountLookupCommand request, CancellationToken ct)
    {
        var ok = await _client.ReprojectStageCountLookupAsync(ct);
        return new ReprojectActionResultDto(
            ProjectionId: "stage-count-lookup",
            Success: ok,
            ChangedRows: 0,
            Detail: ok
                ? "MissionService rebuilt StageCountLookup desde StageService."
                : "MissionService rechazó el reproject o StageService no respondió.",
            CompletedAt: DateTime.UtcNow);
    }
}

public class ReprojectStageLookupCommandHandler
    : IRequestHandler<ReprojectStageLookupCommand, ReprojectActionResultDto>
{
    private readonly IClueServiceSyncClient _client;
    public ReprojectStageLookupCommandHandler(IClueServiceSyncClient client) => _client = client;

    public async Task<ReprojectActionResultDto> Handle(ReprojectStageLookupCommand request, CancellationToken ct)
    {
        var ok = await _client.ReprojectStageLookupAsync(ct);
        return new ReprojectActionResultDto(
            ProjectionId: "stage-lookup",
            Success: ok,
            ChangedRows: 0,
            Detail: ok
                ? "ClueService rebuilt StagesLookup desde StageService."
                : "ClueService rechazó el reproject o StageService no respondió.",
            CompletedAt: DateTime.UtcNow);
    }
}

public class ReprojectRankingForSessionCommandHandler
    : IRequestHandler<ReprojectRankingForSessionCommand, ReprojectActionResultDto>
{
    private readonly ITeamServiceSyncClient _client;
    public ReprojectRankingForSessionCommandHandler(ITeamServiceSyncClient client) => _client = client;

    public async Task<ReprojectActionResultDto> Handle(ReprojectRankingForSessionCommand request, CancellationToken ct)
    {
        var ok = await _client.ReprojectRankingForSessionAsync(request.SessionId, ct);
        return new ReprojectActionResultDto(
            ProjectionId: "ranking-projection",
            Success: ok,
            ChangedRows: 0,
            Detail: ok
                ? $"TeamService rebuilt RankingProjection de la sesión {request.SessionId}."
                : "TeamService rechazó el reproject.",
            CompletedAt: DateTime.UtcNow);
    }
}
