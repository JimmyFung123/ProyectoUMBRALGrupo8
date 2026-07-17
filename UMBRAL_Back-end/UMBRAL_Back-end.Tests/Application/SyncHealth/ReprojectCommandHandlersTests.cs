namespace UMBRAL_Back_end.Tests.Application.SyncHealth;

using FluentAssertions;
using Moq;
using SessionService.Application.SyncHealth;
using SessionService.Application.SyncHealth.Commands.ProxyReprojects;
using SessionService.Application.SyncHealth.Commands.ReconcileStageCompletionRecords;
using SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;
using Xunit;

/// <summary>
/// HU-27 — exercises the reproject and reconcile command handlers.
///
/// The proxy handlers are thin pass-throughs to their sync clients, so the
/// tests focus on three things:
///   1. They forward the call to the right port.
///   2. They translate success/failure into the standard payload.
///   3. The local MissionsLookup reproject refuses to wipe the table when
///      MissionService is unreachable.
/// </summary>
public class ReprojectCommandHandlersTests
{
    // ── ReprojectSessionMissionsLookup ───────────────────────────────────────

    [Fact]
    public async Task SessionMissionsLookup_WhenUpstreamUnreachable_ReturnsFailureAndDoesNotTouchReader()
    {
        var client = new Mock<IMissionServiceSyncClient>();
        client.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync((MissionServiceSyncSnapshot?)null);
        var reader = new Mock<ILocalSyncHealthReader>();
        var handler = new ReprojectSessionMissionsLookupCommandHandler(client.Object, reader.Object);

        var result = await handler.Handle(new ReprojectSessionMissionsLookupCommand(), default);

        result.Success.Should().BeFalse();
        result.ChangedRows.Should().Be(0);
        reader.Verify(r => r.ReprojectMissionsLookupAsync(
            It.IsAny<Func<CancellationToken, Task<IReadOnlyList<UpstreamMissionRow>>>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SessionMissionsLookup_WhenUpstreamReachable_DelegatesRebuildToReader()
    {
        var client = new Mock<IMissionServiceSyncClient>();
        client.Setup(c => c.GetSnapshotAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(new MissionServiceSyncSnapshot(5, 5, 12));
        var reader = new Mock<ILocalSyncHealthReader>();
        reader.Setup(r => r.ReprojectMissionsLookupAsync(
                It.IsAny<Func<CancellationToken, Task<IReadOnlyList<UpstreamMissionRow>>>>(),
                It.IsAny<CancellationToken>()))
              .ReturnsAsync(4);
        var handler = new ReprojectSessionMissionsLookupCommandHandler(client.Object, reader.Object);

        var result = await handler.Handle(new ReprojectSessionMissionsLookupCommand(), default);

        result.Success.Should().BeTrue();
        result.ChangedRows.Should().Be(4);
        result.ProjectionId.Should().Be("missions-lookup-session");
    }

    // ── ReconcileStageCompletionRecords ──────────────────────────────────────

    [Fact]
    public async Task ReconcileStageCompletionRecords_ForwardsToReaderAndReturnsCount()
    {
        var reader = new Mock<ILocalSyncHealthReader>();
        reader.Setup(r => r.ReconcileStageCompletionRecordsAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(7);
        var handler = new ReconcileStageCompletionRecordsCommandHandler(reader.Object);

        var result = await handler.Handle(new ReconcileStageCompletionRecordsCommand(), default);

        result.Success.Should().BeTrue();
        result.ChangedRows.Should().Be(7);
        result.ProjectionId.Should().Be("stage-completion-records");
    }

    // ── Proxy handlers ───────────────────────────────────────────────────────

    [Fact]
    public async Task ReprojectStageMissionsLookup_CallsStageServiceClient()
    {
        var client = new Mock<IStageServiceSyncClient>();
        client.Setup(c => c.ReprojectMissionsLookupAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        var handler = new ReprojectStageMissionsLookupCommandHandler(client.Object);

        var result = await handler.Handle(new ReprojectStageMissionsLookupCommand(), default);

        result.Success.Should().BeTrue();
        result.ProjectionId.Should().Be("missions-lookup-stage");
        client.Verify(c => c.ReprojectMissionsLookupAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReprojectStageCountLookup_TranslatesClientFailureIntoUnsuccessfulResult()
    {
        var client = new Mock<IMissionServiceSyncClient>();
        client.Setup(c => c.ReprojectStageCountLookupAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(false);
        var handler = new ReprojectStageCountLookupCommandHandler(client.Object);

        var result = await handler.Handle(new ReprojectStageCountLookupCommand(), default);

        result.Success.Should().BeFalse();
        result.ProjectionId.Should().Be("stage-count-lookup");
    }

    [Fact]
    public async Task ReprojectStageLookup_CallsClueServiceClient()
    {
        var client = new Mock<IClueServiceSyncClient>();
        client.Setup(c => c.ReprojectStageLookupAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        var handler = new ReprojectStageLookupCommandHandler(client.Object);

        var result = await handler.Handle(new ReprojectStageLookupCommand(), default);

        result.Success.Should().BeTrue();
        result.ProjectionId.Should().Be("stage-lookup");
    }

    [Fact]
    public async Task ReprojectRankingForSession_PassesSessionIdToClient()
    {
        var sessionId = Guid.NewGuid();
        var client = new Mock<ITeamServiceSyncClient>();
        client.Setup(c => c.ReprojectRankingForSessionAsync(sessionId, It.IsAny<CancellationToken>()))
              .ReturnsAsync(true);
        var handler = new ReprojectRankingForSessionCommandHandler(client.Object);

        var result = await handler.Handle(new ReprojectRankingForSessionCommand(sessionId), default);

        result.Success.Should().BeTrue();
        result.ProjectionId.Should().Be("ranking-projection");
        result.Detail.Should().Contain(sessionId.ToString());
        client.Verify(c => c.ReprojectRankingForSessionAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
