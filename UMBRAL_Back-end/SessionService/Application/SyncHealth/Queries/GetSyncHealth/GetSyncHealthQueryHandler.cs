namespace SessionService.Application.SyncHealth.Queries.GetSyncHealth;

using MediatR;

/// <summary>
/// HU-27 aggregator. Fans out to every downstream service in parallel, joins
/// the snapshots with local data and turns the result into a list of
/// <see cref="ProjectionHealthDto"/> entries — one per CQRS read model in the
/// system.
///
/// When a downstream service is unreachable, its card is still emitted but
/// flagged as <c>Critical</c> with an explanatory detail string, so the admin
/// can spot the broken service without the dashboard crashing.
/// </summary>
public class GetSyncHealthQueryHandler : IRequestHandler<GetSyncHealthQuery, SyncHealthDto>
{
    private readonly IMissionServiceSyncClient _missionClient;
    private readonly IStageServiceSyncClient _stageClient;
    private readonly IClueServiceSyncClient _clueClient;
    private readonly ITeamServiceSyncClient _teamClient;
    private readonly ILocalSyncHealthReader _localReader;

    public GetSyncHealthQueryHandler(
        IMissionServiceSyncClient missionClient,
        IStageServiceSyncClient stageClient,
        IClueServiceSyncClient clueClient,
        ITeamServiceSyncClient teamClient,
        ILocalSyncHealthReader localReader)
    {
        _missionClient = missionClient;
        _stageClient = stageClient;
        _clueClient = clueClient;
        _teamClient = teamClient;
        _localReader = localReader;
    }

    public async Task<SyncHealthDto> Handle(GetSyncHealthQuery request, CancellationToken cancellationToken)
    {
        var missionTask = _missionClient.GetSnapshotAsync(cancellationToken);
        var stageTask   = _stageClient.GetSnapshotAsync(cancellationToken);
        var clueTask    = _clueClient.GetSnapshotAsync(cancellationToken);
        var teamTask    = _teamClient.GetSnapshotAsync(cancellationToken);
        var localTask   = _localReader.ReadAsync(cancellationToken);

        await Task.WhenAll(missionTask, stageTask, clueTask, teamTask, localTask);

        var mission = await missionTask;
        var stage   = await stageTask;
        var clue    = await clueTask;
        var team    = await teamTask;
        var local   = await localTask;

        var now = DateTime.UtcNow;
        var projections = new List<ProjectionHealthDto>
        {
            BuildMissionsLookupSession(mission, local, now),
            BuildMissionsLookupStage(mission, stage, now),
            BuildStageCountLookup(mission, stage),
            BuildStageLookup(stage, clue, now),
            BuildRankingProjection(team, local, now),
            BuildStageCompletionRecords(local),
        };

        return new SyncHealthDto(GeneratedAt: now, Projections: projections);
    }

    // ── Card 1: SessionService.MissionsLookup ────────────────────────────────
    private static ProjectionHealthDto BuildMissionsLookupSession(
        MissionServiceSyncSnapshot? mission,
        LocalSessionsSnapshot local,
        DateTime now)
    {
        if (mission is null)
            return Unreachable(
                "missions-lookup-session", "MissionsLookup (SessionService)",
                "SessionService", "MissionService.Missions", "SessionService.MissionsLookup",
                local.MissionsLookupCount, local.MissionsLookupMaxUpdatedAt, now,
                supportsReproject: true);

        var hasDrift = mission.TotalMissions != local.MissionsLookupCount;
        var lag = SyncHealthClassifier.ComputeLagSeconds(local.MissionsLookupMaxUpdatedAt, now);
        var status = SyncHealthClassifier.Classify(lag, hasDrift);
        var detail = hasDrift
            ? $"Drift detectado: MissionService reporta {mission.TotalMissions} misiones, la réplica tiene {local.MissionsLookupCount}."
            : $"{mission.TotalMissions} misiones replicadas correctamente.";

        return new ProjectionHealthDto(
            ProjectionId: "missions-lookup-session",
            DisplayName: "MissionsLookup (SessionService)",
            OwningService: "SessionService",
            SourceModel: "MissionService.Missions",
            ReadModel: "SessionService.MissionsLookup",
            SourceCount: mission.TotalMissions,
            ReadCount: local.MissionsLookupCount,
            LastUpdatedAt: local.MissionsLookupMaxUpdatedAt,
            LagSeconds: lag,
            Status: status,
            Detail: detail,
            SupportsReproject: true,
            RequiresSessionId: false,
            Sessions: null);
    }

    // ── Card 2: StageService.MissionsLookup ──────────────────────────────────
    private static ProjectionHealthDto BuildMissionsLookupStage(
        MissionServiceSyncSnapshot? mission,
        StageServiceSyncSnapshot? stage,
        DateTime now)
    {
        if (stage is null)
            return Unreachable(
                "missions-lookup-stage", "MissionsLookup (StageService)",
                "StageService", "MissionService.Missions", "StageService.MissionsLookup",
                0, null, now, supportsReproject: true);

        var sourceCount = mission?.TotalMissions ?? 0;
        var hasDrift = mission is not null && sourceCount != stage.MissionsLookupCount;
        var lag = SyncHealthClassifier.ComputeLagSeconds(stage.MissionsLookupMaxUpdatedAt, now);
        var status = SyncHealthClassifier.Classify(lag, hasDrift);
        var detail = mission is null
            ? $"MissionService no responde, no se puede comparar counts. Réplica tiene {stage.MissionsLookupCount} entradas."
            : (hasDrift
                ? $"Drift detectado: MissionService reporta {sourceCount} misiones, StageService tiene {stage.MissionsLookupCount}."
                : $"{sourceCount} misiones replicadas correctamente.");

        return new ProjectionHealthDto(
            ProjectionId: "missions-lookup-stage",
            DisplayName: "MissionsLookup (StageService)",
            OwningService: "StageService",
            SourceModel: "MissionService.Missions",
            ReadModel: "StageService.MissionsLookup",
            SourceCount: sourceCount,
            ReadCount: stage.MissionsLookupCount,
            LastUpdatedAt: stage.MissionsLookupMaxUpdatedAt,
            LagSeconds: lag,
            Status: status,
            Detail: detail,
            SupportsReproject: true,
            RequiresSessionId: false,
            Sessions: null);
    }

    // ── Card 3: MissionService.StageCountLookup ──────────────────────────────
    private static ProjectionHealthDto BuildStageCountLookup(
        MissionServiceSyncSnapshot? mission,
        StageServiceSyncSnapshot? stage)
    {
        if (mission is null)
            return new ProjectionHealthDto(
                ProjectionId: "stage-count-lookup",
                DisplayName: "StageCountLookup (MissionService)",
                OwningService: "MissionService",
                SourceModel: "StageService.Stages",
                ReadModel: "MissionService.StageCountLookup",
                SourceCount: stage?.TotalStages ?? 0,
                ReadCount: 0,
                LastUpdatedAt: null,
                LagSeconds: null,
                Status: SyncHealthClassifier.Critical,
                Detail: "MissionService no responde.",
                SupportsReproject: true,
                RequiresSessionId: false,
                Sessions: null);

        // No timestamp on StageCountLookup — rely purely on count comparison.
        var totalUpstream = stage?.TotalStages ?? 0;
        var hasDrift = stage is not null && totalUpstream != mission.StageCountLookupSum;
        var status = SyncHealthClassifier.Classify(lagSeconds: null, hasDrift: hasDrift);
        var detail = stage is null
            ? $"StageService no responde, no se puede comparar. Suma actual: {mission.StageCountLookupSum} (en {mission.StageCountLookupRows} misiones)."
            : (hasDrift
                ? $"Drift detectado: StageService reporta {totalUpstream} etapas, la suma de StageCountLookup es {mission.StageCountLookupSum}."
                : $"{mission.StageCountLookupSum} etapas en {mission.StageCountLookupRows} misiones — alineado con StageService.");

        return new ProjectionHealthDto(
            ProjectionId: "stage-count-lookup",
            DisplayName: "StageCountLookup (MissionService)",
            OwningService: "MissionService",
            SourceModel: "StageService.Stages",
            ReadModel: "MissionService.StageCountLookup",
            SourceCount: totalUpstream,
            ReadCount: mission.StageCountLookupSum,
            LastUpdatedAt: null,
            LagSeconds: null,
            Status: status,
            Detail: detail,
            SupportsReproject: true,
            RequiresSessionId: false,
            Sessions: null);
    }

    // ── Card 4: ClueService.StageLookup ──────────────────────────────────────
    private static ProjectionHealthDto BuildStageLookup(
        StageServiceSyncSnapshot? stage,
        ClueServiceSyncSnapshot? clue,
        DateTime now)
    {
        if (clue is null)
            return Unreachable(
                "stage-lookup", "StageLookup (ClueService)",
                "ClueService", "StageService.Stages", "ClueService.StagesLookup",
                0, null, now, supportsReproject: true);

        var sourceCount = stage?.TotalStages ?? 0;
        var hasDrift = stage is not null && sourceCount != clue.StagesLookupCount;
        var lag = SyncHealthClassifier.ComputeLagSeconds(clue.StagesLookupMaxCreatedAt, now);
        var status = SyncHealthClassifier.Classify(lag, hasDrift);
        var detail = stage is null
            ? $"StageService no responde, no se puede comparar counts. Réplica tiene {clue.StagesLookupCount} entradas."
            : (hasDrift
                ? $"Drift detectado: StageService reporta {sourceCount} etapas, ClueService tiene {clue.StagesLookupCount}."
                : $"{sourceCount} etapas replicadas correctamente.");

        return new ProjectionHealthDto(
            ProjectionId: "stage-lookup",
            DisplayName: "StageLookup (ClueService)",
            OwningService: "ClueService",
            SourceModel: "StageService.Stages",
            ReadModel: "ClueService.StagesLookup",
            SourceCount: sourceCount,
            ReadCount: clue.StagesLookupCount,
            LastUpdatedAt: clue.StagesLookupMaxCreatedAt,
            LagSeconds: lag,
            Status: status,
            Detail: detail,
            SupportsReproject: true,
            RequiresSessionId: false,
            Sessions: null);
    }

    // ── Card 5: TeamService.RankingProjection (per session) ──────────────────
    private static ProjectionHealthDto BuildRankingProjection(
        TeamServiceSyncSnapshot? team,
        LocalSessionsSnapshot local,
        DateTime now)
    {
        if (team is null)
            return new ProjectionHealthDto(
                ProjectionId: "ranking-projection",
                DisplayName: "RankingProjection (TeamService)",
                OwningService: "TeamService",
                SourceModel: "TeamService.Teams",
                ReadModel: "TeamService.RankingProjections",
                SourceCount: 0,
                ReadCount: 0,
                LastUpdatedAt: null,
                LagSeconds: null,
                Status: SyncHealthClassifier.Critical,
                Detail: "TeamService no responde.",
                SupportsReproject: true,
                RequiresSessionId: true,
                Sessions: Array.Empty<RankingProjectionSessionDto>());

        var sessions = team.Sessions
            .Select(s =>
            {
                var hasDrift = s.TeamCount != s.ProjectionCount;
                var lag = SyncHealthClassifier.ComputeLagSeconds(s.LastUpdatedAt, now);
                var status = SyncHealthClassifier.Classify(lag, hasDrift);
                local.SessionStatusById.TryGetValue(s.SessionId, out var sessionStatus);
                return new RankingProjectionSessionDto(
                    SessionId: s.SessionId,
                    SessionStatus: sessionStatus ?? "Unknown",
                    TeamCount: s.TeamCount,
                    ProjectionCount: s.ProjectionCount,
                    LastUpdatedAt: s.LastUpdatedAt,
                    LagSeconds: lag,
                    Status: status);
            })
            .ToList();

        var worstStatus = sessions.Count == 0
            ? SyncHealthClassifier.Healthy
            : (sessions.Any(s => s.Status == SyncHealthClassifier.Critical) ? SyncHealthClassifier.Critical
                : sessions.Any(s => s.Status == SyncHealthClassifier.Warning) ? SyncHealthClassifier.Warning
                : SyncHealthClassifier.Healthy);

        var detail = sessions.Count == 0
            ? "Sin sesiones con equipos o proyecciones."
            : $"{sessions.Count} sesión(es) monitoreadas. Estado del peor caso: {worstStatus}.";

        // For the parent card we report aggregated counts.
        return new ProjectionHealthDto(
            ProjectionId: "ranking-projection",
            DisplayName: "RankingProjection (TeamService)",
            OwningService: "TeamService",
            SourceModel: "TeamService.Teams",
            ReadModel: "TeamService.RankingProjections",
            SourceCount: team.TotalTeams,
            ReadCount: team.TotalProjections,
            LastUpdatedAt: sessions
                .Where(s => s.LastUpdatedAt.HasValue)
                .Select(s => s.LastUpdatedAt!.Value)
                .DefaultIfEmpty(default)
                .Max() is var maxDate && maxDate == default ? null : maxDate,
            LagSeconds: null,                       // per-session lag is more meaningful; admins act on rows.
            Status: worstStatus,
            Detail: detail,
            SupportsReproject: true,
            RequiresSessionId: true,
            Sessions: sessions);
    }

    // ── Card 6: SessionService.StageCompletionRecords (flag consistency) ─────
    private static ProjectionHealthDto BuildStageCompletionRecords(LocalSessionsSnapshot local)
    {
        var hasDrift = local.StageCompletionRecordsFlagDrift > 0;
        var status = SyncHealthClassifier.Classify(lagSeconds: null, hasDrift: hasDrift);
        var detail = hasDrift
            ? $"{local.StageCompletionRecordsFlagDrift} fila(s) marcadas como invisibles aunque su sesión ya está finalizada."
            : $"{local.StageCompletionRecordsTotal} registros, flag consistente con el estado de cada sesión.";

        return new ProjectionHealthDto(
            ProjectionId: "stage-completion-records",
            DisplayName: "StageCompletionRecords (SessionService)",
            OwningService: "SessionService",
            SourceModel: "SessionService.Sessions (estado)",
            ReadModel: "SessionService.StageCompletionRecords",
            SourceCount: local.StageCompletionRecordsTotal,
            ReadCount: local.StageCompletionRecordsTotal - local.StageCompletionRecordsFlagDrift,
            LastUpdatedAt: null,
            LagSeconds: null,
            Status: status,
            Detail: detail,
            SupportsReproject: true,
            RequiresSessionId: false,
            Sessions: null);
    }

    private static ProjectionHealthDto Unreachable(
        string id, string displayName, string owningService,
        string sourceModel, string readModel,
        int readCount, DateTime? lastUpdated, DateTime now,
        bool supportsReproject)
    {
        var lag = SyncHealthClassifier.ComputeLagSeconds(lastUpdated, now);
        return new ProjectionHealthDto(
            ProjectionId: id,
            DisplayName: displayName,
            OwningService: owningService,
            SourceModel: sourceModel,
            ReadModel: readModel,
            SourceCount: 0,
            ReadCount: readCount,
            LastUpdatedAt: lastUpdated,
            LagSeconds: lag,
            Status: SyncHealthClassifier.Critical,
            Detail: $"{owningService} no responde — no se puede verificar el estado del modelo de lectura.",
            SupportsReproject: supportsReproject,
            RequiresSessionId: false,
            Sessions: null);
    }
}
