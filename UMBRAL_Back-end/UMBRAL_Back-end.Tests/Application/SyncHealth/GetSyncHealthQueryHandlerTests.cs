namespace UMBRAL_Back_end.Tests.Application.SyncHealth;

using FluentAssertions;
using Moq;
using SessionService.Application.SyncHealth;
using SessionService.Application.SyncHealth.Queries.GetSyncHealth;
using Xunit;

/// <summary>
/// HU-27 — verifies the aggregator handler composes one card per projection,
/// flags drift correctly and survives downstream services being unreachable.
/// </summary>
public class GetSyncHealthQueryHandlerTests
{
    private readonly Mock<IMissionServiceSyncClient> _missionClient = new();
    private readonly Mock<IStageServiceSyncClient> _stageClient = new();
    private readonly Mock<IClueServiceSyncClient> _clueClient = new();
    private readonly Mock<ITeamServiceSyncClient> _teamClient = new();
    private readonly Mock<ILocalSyncHealthReader> _localReader = new();
    private readonly GetSyncHealthQueryHandler _handler;

    public GetSyncHealthQueryHandlerTests()
    {
        _handler = new GetSyncHealthQueryHandler(
            _missionClient.Object,
            _stageClient.Object,
            _clueClient.Object,
            _teamClient.Object,
            _localReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsExactlySixProjections_OneForEachReadModel()
    {
        WireUpHealthyState();

        var result = await _handler.Handle(new GetSyncHealthQuery(), default);

        result.Projections.Should().HaveCount(6);
        result.Projections.Select(p => p.ProjectionId).Should().BeEquivalentTo(new[]
        {
            "missions-lookup-session",
            "missions-lookup-stage",
            "stage-count-lookup",
            "stage-lookup",
            "ranking-projection",
            "stage-completion-records",
        });
    }

    [Fact]
    public async Task Handle_WhenAllInSync_AllCardsAreHealthy()
    {
        WireUpHealthyState();

        var result = await _handler.Handle(new GetSyncHealthQuery(), default);

        result.Projections.Should().OnlyContain(p => p.Status == "Healthy");
    }

    [Fact]
    public async Task Handle_WhenSessionsReplicaHasFewerRows_MissionsLookupSessionIsCritical()
    {
        WireUpHealthyState();
        // SessionService's MissionsLookup has 7 rows but MissionService says 10.
        _localReader.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalSessionsSnapshot(
                MissionsLookupCount: 7,
                MissionsLookupMaxUpdatedAt: DateTime.UtcNow,
                StageCompletionRecordsTotal: 0,
                StageCompletionRecordsFlagDrift: 0,
                SessionStatusById: new Dictionary<Guid, string>()));

        var result = await _handler.Handle(new GetSyncHealthQuery(), default);

        var card = result.Projections.Single(p => p.ProjectionId == "missions-lookup-session");
        card.Status.Should().Be("Critical");
        card.Detail.Should().Contain("Drift");
    }

    [Fact]
    public async Task Handle_WhenMissionServiceUnreachable_DependentCardsRemainCriticalButQueryStillSucceeds()
    {
        WireUpHealthyState();
        _missionClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionServiceSyncSnapshot?)null);

        var result = await _handler.Handle(new GetSyncHealthQuery(), default);

        // The handler must still emit all six cards.
        result.Projections.Should().HaveCount(6);
        // The MissionsLookup cards and StageCountLookup card go red.
        result.Projections.Single(p => p.ProjectionId == "missions-lookup-session").Status.Should().Be("Critical");
        result.Projections.Single(p => p.ProjectionId == "stage-count-lookup").Status.Should().Be("Critical");
        // Cards unrelated to MissionService stay healthy.
        result.Projections.Single(p => p.ProjectionId == "stage-lookup").Status.Should().Be("Healthy");
    }

    [Fact]
    public async Task Handle_RankingProjection_ReturnsPerSessionRowsAndUsesWorstStatus()
    {
        var goodSession = Guid.NewGuid();
        var driftSession = Guid.NewGuid();

        _missionClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MissionServiceSyncSnapshot(0, 0, 0));
        _stageClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageServiceSyncSnapshot(0, 0, null));
        _clueClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClueServiceSyncSnapshot(0, 0, null));
        _teamClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamServiceSyncSnapshot(
                TotalTeams: 6,
                TotalProjections: 5,
                Sessions:
                [
                    new TeamServiceSyncSessionRow(goodSession, 3, 3, DateTime.UtcNow),
                    new TeamServiceSyncSessionRow(driftSession, 3, 2, DateTime.UtcNow), // drift!
                ]));
        _localReader.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalSessionsSnapshot(0, null, 0, 0,
                new Dictionary<Guid, string>
                {
                    [goodSession] = "InProgress",
                    [driftSession] = "InProgress",
                }));

        var result = await _handler.Handle(new GetSyncHealthQuery(), default);

        var ranking = result.Projections.Single(p => p.ProjectionId == "ranking-projection");
        ranking.Sessions.Should().NotBeNull();
        ranking.Sessions!.Should().HaveCount(2);
        ranking.Sessions.Single(s => s.SessionId == driftSession).Status.Should().Be("Critical");
        ranking.Sessions.Single(s => s.SessionId == goodSession).Status.Should().Be("Healthy");
        ranking.Status.Should().Be("Critical"); // worst-of aggregation
    }

    [Fact]
    public async Task Handle_FlagDriftOnStageCompletionRecords_BecomesCritical()
    {
        WireUpHealthyState();
        _localReader.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalSessionsSnapshot(
                MissionsLookupCount: 10,
                MissionsLookupMaxUpdatedAt: DateTime.UtcNow,
                StageCompletionRecordsTotal: 50,
                StageCompletionRecordsFlagDrift: 3,   // 3 finalized rows still hidden
                SessionStatusById: new Dictionary<Guid, string>()));

        var result = await _handler.Handle(new GetSyncHealthQuery(), default);

        var card = result.Projections.Single(p => p.ProjectionId == "stage-completion-records");
        card.Status.Should().Be("Critical");
        card.Detail.Should().Contain("3");
    }

    // ── helper ────────────────────────────────────────────────────────────────

    private void WireUpHealthyState()
    {
        _missionClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MissionServiceSyncSnapshot(
                TotalMissions: 10,
                StageCountLookupRows: 10,
                StageCountLookupSum: 30));
        _stageClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageServiceSyncSnapshot(
                TotalStages: 30,
                MissionsLookupCount: 10,
                MissionsLookupMaxUpdatedAt: DateTime.UtcNow));
        _clueClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClueServiceSyncSnapshot(
                TotalClues: 60,
                StagesLookupCount: 30,
                StagesLookupMaxCreatedAt: DateTime.UtcNow));
        _teamClient.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamServiceSyncSnapshot(
                TotalTeams: 0,
                TotalProjections: 0,
                Sessions: Array.Empty<TeamServiceSyncSessionRow>()));
        _localReader.Setup(r => r.ReadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LocalSessionsSnapshot(
                MissionsLookupCount: 10,
                MissionsLookupMaxUpdatedAt: DateTime.UtcNow,
                StageCompletionRecordsTotal: 0,
                StageCompletionRecordsFlagDrift: 0,
                SessionStatusById: new Dictionary<Guid, string>()));
    }
}
