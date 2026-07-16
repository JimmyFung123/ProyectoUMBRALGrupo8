// Los cuatro *SyncClient viven en el ensamblado de SessionService (extern alias, ver el .csproj).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.ExternalClients;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SessionServiceAssembly::SessionService.Infrastructure.ExternalClients;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Ejercita los cuatro adaptadores HTTP del dashboard de sync-health (HU-27) contra un
/// UpstreamJsonStub real: el GET de snapshot (null si el servicio no responde) y el POST
/// de reproject (false si falla). Es la capa Infrastructure que el aggregator no ejercita
/// porque usa fakes/stubs de las interfaces.
/// </summary>
public class SyncHealthClientsTests
{
    // ── MissionServiceSyncClient ─────────────────────────────────────────────

    [Fact]
    public async Task MissionSync_GetSnapshot_ReturnsSnapshot_OnSuccess()
    {
        await using var stub = await StubHttp.Returning("{}");
        var result = await new MissionServiceSyncClient(StubHttp.ClientTo(stub)).GetSnapshotAsync(CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task MissionSync_GetSnapshot_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 500);
        var result = await new MissionServiceSyncClient(StubHttp.ClientTo(stub)).GetSnapshotAsync(CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task MissionSync_GetMissions_MapsRows_OnSuccess()
    {
        var missionId = Guid.NewGuid();
        await using var stub = await StubHttp.Returning($$"""
            [{"id":"{{missionId}}","name":"Misión 1","status":"Active"}]
            """);
        var result = await new MissionServiceSyncClient(StubHttp.ClientTo(stub)).GetMissionsAsync(CancellationToken.None);
        result.Should().ContainSingle();
        result[0].Id.Should().Be(missionId);
        result[0].Name.Should().Be("Misión 1");
        result[0].Status.Should().Be("Active");
    }

    [Fact]
    public async Task MissionSync_GetMissions_ReturnsEmpty_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("[]", statusCode: 500);
        var result = await new MissionServiceSyncClient(StubHttp.ClientTo(stub)).GetMissionsAsync(CancellationToken.None);
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task MissionSync_Reproject_ReturnsTrue_OnSuccess()
    {
        await using var stub = await StubHttp.Returning("{}");
        var result = await new MissionServiceSyncClient(StubHttp.ClientTo(stub)).ReprojectStageCountLookupAsync(CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task MissionSync_Reproject_ReturnsFalse_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 500);
        var result = await new MissionServiceSyncClient(StubHttp.ClientTo(stub)).ReprojectStageCountLookupAsync(CancellationToken.None);
        result.Should().BeFalse();
    }

    // ── StageServiceSyncClient ───────────────────────────────────────────────

    [Fact]
    public async Task StageSync_GetSnapshot_ReturnsSnapshot_OnSuccess()
    {
        await using var stub = await StubHttp.Returning("{}");
        var result = await new StageServiceSyncClient(StubHttp.ClientTo(stub)).GetSnapshotAsync(CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task StageSync_GetSnapshot_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 503);
        var result = await new StageServiceSyncClient(StubHttp.ClientTo(stub)).GetSnapshotAsync(CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task StageSync_Reproject_ReturnsTrue_OnSuccess()
    {
        await using var stub = await StubHttp.Returning("{}");
        var result = await new StageServiceSyncClient(StubHttp.ClientTo(stub)).ReprojectMissionsLookupAsync(CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task StageSync_Reproject_ReturnsFalse_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 500);
        var result = await new StageServiceSyncClient(StubHttp.ClientTo(stub)).ReprojectMissionsLookupAsync(CancellationToken.None);
        result.Should().BeFalse();
    }

    // ── ClueServiceSyncClient ────────────────────────────────────────────────

    [Fact]
    public async Task ClueSync_GetSnapshot_ReturnsSnapshot_OnSuccess()
    {
        await using var stub = await StubHttp.Returning("{}");
        var result = await new ClueServiceSyncClient(StubHttp.ClientTo(stub)).GetSnapshotAsync(CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ClueSync_GetSnapshot_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 500);
        var result = await new ClueServiceSyncClient(StubHttp.ClientTo(stub)).GetSnapshotAsync(CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task ClueSync_Reproject_ReturnsTrue_OnSuccess()
    {
        await using var stub = await StubHttp.Returning("{}");
        var result = await new ClueServiceSyncClient(StubHttp.ClientTo(stub)).ReprojectStageLookupAsync(CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ClueSync_Reproject_ReturnsFalse_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 500);
        var result = await new ClueServiceSyncClient(StubHttp.ClientTo(stub)).ReprojectStageLookupAsync(CancellationToken.None);
        result.Should().BeFalse();
    }

    // ── TeamServiceSyncClient ────────────────────────────────────────────────

    [Fact]
    public async Task TeamSync_GetSnapshot_ReturnsSnapshot_OnSuccess()
    {
        await using var stub = await StubHttp.Returning("{}");
        var result = await new TeamServiceSyncClient(StubHttp.ClientTo(stub)).GetSnapshotAsync(CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task TeamSync_GetSnapshot_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 500);
        var result = await new TeamServiceSyncClient(StubHttp.ClientTo(stub)).GetSnapshotAsync(CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task TeamSync_ReprojectRanking_ReturnsTrue_OnSuccess()
    {
        await using var stub = await StubHttp.Returning("{}");
        var result = await new TeamServiceSyncClient(StubHttp.ClientTo(stub)).ReprojectRankingForSessionAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TeamSync_ReprojectRanking_ReturnsFalse_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 500);
        var result = await new TeamServiceSyncClient(StubHttp.ClientTo(stub)).ReprojectRankingForSessionAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeFalse();
    }
}
