// TeamServiceClient vive en el ensamblado de SessionService, referenciado con extern
// alias porque cada servicio genera un Program global que colisiona (ver el .csproj).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.ExternalClients;

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SessionServiceAssembly::SessionService.Infrastructure.ExternalClients;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Ejercita el <see cref="TeamServiceClient"/> real (no el fake que la DI inyecta en los
/// tests de controller): levanta un <see cref="UpstreamJsonStub"/> haciendo de TeamService y
/// verifica que el cliente arma el request, parsea el JSON de respuesta y aplica los valores
/// "seguros" de las ramas no-2xx / catch. Es la capa Infrastructure de salida HTTP que los
/// tests de SessionsController/TeamsController dejan sin ejercitar al sustituir el cliente.
/// No usa fixtures de Postgres/RabbitMQ: el cliente solo necesita un HttpClient.
/// </summary>
public class TeamServiceClientTests
{
    private static TeamServiceClient ClientFor(UpstreamJsonStub stub) =>
        new(new HttpClient { BaseAddress = new Uri(stub.BaseUrl.TrimEnd('/') + "/") });

    private static async Task<UpstreamJsonStub> StubReturning(string json, int statusCode = 200)
    {
        var stub = new UpstreamJsonStub();
        await stub.StartAsync(json, statusCode);
        return stub;
    }

    // ── HasEnrolledTeamsAsync ────────────────────────────────────────────────

    [Fact]
    public async Task HasEnrolledTeams_ReturnsTrue_WhenUpstreamReturnsANonEmptyArray()
    {
        await using var stub = await StubReturning("[{}, {}]");
        var result = await ClientFor(stub).HasEnrolledTeamsAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasEnrolledTeams_ReturnsFalse_WhenUpstreamReturnsEmptyArray()
    {
        await using var stub = await StubReturning("[]");
        var result = await ClientFor(stub).HasEnrolledTeamsAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasEnrolledTeams_ReturnsFalse_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("[]", statusCode: 500);
        var result = await ClientFor(stub).HasEnrolledTeamsAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasEnrolledTeams_ReturnsFalse_WhenBodyIsNotValidJson()
    {
        await using var stub = await StubReturning("no-soy-json");
        var result = await ClientFor(stub).HasEnrolledTeamsAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeFalse();
    }

    // ── AllTeamsMeetMinimumMembersAsync ──────────────────────────────────────

    [Fact]
    public async Task AllTeamsMeetMinimum_ReturnsTrue_WhenEveryTeamMeetsTheMinimum()
    {
        await using var stub = await StubReturning("""[{"memberCount":3},{"memberCount":2}]""");
        var result = await ClientFor(stub).AllTeamsMeetMinimumMembersAsync(Guid.NewGuid(), 2, CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task AllTeamsMeetMinimum_ReturnsFalse_WhenAnyTeamIsBelowTheMinimum()
    {
        await using var stub = await StubReturning("""[{"memberCount":3},{"memberCount":1}]""");
        var result = await ClientFor(stub).AllTeamsMeetMinimumMembersAsync(Guid.NewGuid(), 2, CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AllTeamsMeetMinimum_ReturnsFalse_WhenThereAreNoTeams()
    {
        await using var stub = await StubReturning("[]");
        var result = await ClientFor(stub).AllTeamsMeetMinimumMembersAsync(Guid.NewGuid(), 2, CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AllTeamsMeetMinimum_ReturnsFalse_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("[]", statusCode: 503);
        var result = await ClientFor(stub).AllTeamsMeetMinimumMembersAsync(Guid.NewGuid(), 2, CancellationToken.None);
        result.Should().BeFalse();
    }

    // ── ReleaseClueAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReleaseClue_ReturnsCluesReceived_OnSuccess()
    {
        await using var stub = await StubReturning("""{"cluesReceived":3}""");
        var result = await ClientFor(stub).ReleaseClueAsync(Guid.NewGuid(), totalCluesForStage: 5, CancellationToken.None);
        result.Should().Be(3);
    }

    [Fact]
    public async Task ReleaseClue_ReturnsMinusOne_WhenAllCluesAlreadyExhausted()
    {
        await using var stub = await StubReturning("{}", statusCode: 409);
        var result = await ClientFor(stub).ReleaseClueAsync(Guid.NewGuid(), totalCluesForStage: 5, CancellationToken.None);
        result.Should().Be(-1);
    }

    [Fact]
    public async Task ReleaseClue_ReturnsMinusOne_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("{}", statusCode: 500);
        var result = await ClientFor(stub).ReleaseClueAsync(Guid.NewGuid(), totalCluesForStage: 5, CancellationToken.None);
        result.Should().Be(-1);
    }

    // ── GetTeamProgressAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetTeamProgress_MapsUpstreamItems()
    {
        var teamId = Guid.NewGuid();
        await using var stub = await StubReturning($$"""
            [{"id":"{{teamId}}","name":"Los Lobos","currentStageOrder":2,
              "cluesReceivedCurrentStage":1,"clueTimerResetAt":null,"lastClueWasAutomatic":true,
              "memberCount":4}]
            """);
        var result = await ClientFor(stub).GetTeamProgressAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().ContainSingle();
        result[0].Id.Should().Be(teamId);
        result[0].Name.Should().Be("Los Lobos");
        result[0].CurrentStageOrder.Should().Be(2);
        result[0].CluesReceivedCurrentStage.Should().Be(1);
        result[0].LastClueWasAutomatic.Should().BeTrue();
    }

    [Fact]
    public async Task GetTeamProgress_ReturnsEmpty_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("[]", statusCode: 500);
        var result = await ClientFor(stub).GetTeamProgressAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeEmpty();
    }

    // ── PenalizeTeamAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task Penalize_ReturnsNewScore_OnSuccess()
    {
        await using var stub = await StubReturning("""{"newScore":80}""");
        var result = await ClientFor(stub).PenalizeTeamAsync(Guid.NewGuid(), 20, "retraso", CancellationToken.None);
        result.Should().Be(80);
    }

    [Fact]
    public async Task Penalize_ReturnsIntMinValue_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("{}", statusCode: 500);
        var result = await ClientFor(stub).PenalizeTeamAsync(Guid.NewGuid(), 20, "retraso", CancellationToken.None);
        result.Should().Be(int.MinValue);
    }

    // ── LeaveTeamAsync (best-effort, no lanza) ───────────────────────────────

    [Fact]
    public async Task Leave_DoesNotThrow_OnSuccess()
    {
        await using var stub = await StubReturning("{}");
        var act = async () => await ClientFor(stub).LeaveTeamAsync(Guid.NewGuid(), CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    // ── ForceAdvanceTeamAsync ────────────────────────────────────────────────

    [Fact]
    public async Task ForceAdvance_ReturnsTransition_OnSuccess()
    {
        await using var stub = await StubReturning("""{"newScore":10,"elapsedSeconds":42}""");
        var result = await ClientFor(stub).ForceAdvanceTeamAsync(Guid.NewGuid(), 3, CancellationToken.None);
        result.Should().NotBeNull();
        result!.NewScore.Should().Be(10);
        result.ElapsedSeconds.Should().Be(42);
    }

    [Fact]
    public async Task ForceAdvance_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("{}", statusCode: 500);
        var result = await ClientFor(stub).ForceAdvanceTeamAsync(Guid.NewGuid(), 3, CancellationToken.None);
        result.Should().BeNull();
    }

    // ── RecordEvidenceOutcomeAsync ───────────────────────────────────────────

    [Fact]
    public async Task RecordEvidenceOutcome_ReturnsTransition_OnSuccess()
    {
        await using var stub = await StubReturning("""{"newScore":120,"elapsedSeconds":15}""");
        var result = await ClientFor(stub).RecordEvidenceOutcomeAsync(Guid.NewGuid(), true, 30, 4, CancellationToken.None);
        result.Should().NotBeNull();
        result!.NewScore.Should().Be(120);
        result.ElapsedSeconds.Should().Be(15);
    }

    [Fact]
    public async Task RecordEvidenceOutcome_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("{}", statusCode: 500);
        var result = await ClientFor(stub).RecordEvidenceOutcomeAsync(Guid.NewGuid(), true, 30, 4, CancellationToken.None);
        result.Should().BeNull();
    }

    // ── RecordWrongAttemptAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RecordWrongAttempt_ReturnsResult_OnSuccess()
    {
        await using var stub = await StubReturning("""{"newWrongCount":2,"newScore":40}""");
        var result = await ClientFor(stub).RecordWrongAttemptAsync(Guid.NewGuid(), Guid.NewGuid(), 10, CancellationToken.None);
        result.Should().NotBeNull();
        result!.NewWrongCount.Should().Be(2);
        result.NewScore.Should().Be(40);
    }

    [Fact]
    public async Task RecordWrongAttempt_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("{}", statusCode: 500);
        var result = await ClientFor(stub).RecordWrongAttemptAsync(Guid.NewGuid(), Guid.NewGuid(), 10, CancellationToken.None);
        result.Should().BeNull();
    }

    // ── GetTeamByIdAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetTeamById_MapsUpstreamItem_OnSuccess()
    {
        var teamId = Guid.NewGuid();
        await using var stub = await StubReturning($$"""
            {"teamId":"{{teamId}}","teamName":"Los Lobos","inviteCode":"ABC123",
             "memberCount":4,"currentStageOrder":2}
            """);
        var result = await ClientFor(stub).GetTeamByIdAsync(teamId, CancellationToken.None);
        result.Should().NotBeNull();
        result!.Id.Should().Be(teamId);
        result.Name.Should().Be("Los Lobos");
        result.CurrentStageOrder.Should().Be(2);
    }

    [Fact]
    public async Task GetTeamById_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("{}", statusCode: 404);
        var result = await ClientFor(stub).GetTeamByIdAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeNull();
    }

    // ── GetSessionRankingAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetSessionRanking_MapsSnapshot_OnSuccess()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        await using var stub = await StubReturning($$"""
            {"sessionId":"{{sessionId}}","generatedAt":"2026-07-15T00:00:00Z",
             "teams":[{"teamId":"{{teamId}}","name":"Los Lobos","score":150,"rank":1,
                        "currentStageOrder":3,"isConnected":true,"lastStageCompletedAt":null}]}
            """);
        var result = await ClientFor(stub).GetSessionRankingAsync(sessionId, CancellationToken.None);
        result.Should().NotBeNull();
        result!.SessionId.Should().Be(sessionId);
        result.Teams.Should().ContainSingle();
        result.Teams[0].TeamId.Should().Be(teamId);
        result.Teams[0].Score.Should().Be(150);
        result.Teams[0].Rank.Should().Be(1);
        result.Teams[0].IsConnected.Should().BeTrue();
    }

    [Fact]
    public async Task GetSessionRanking_ReturnsNull_OnNonSuccessStatus()
    {
        await using var stub = await StubReturning("{}", statusCode: 500);
        var result = await ClientFor(stub).GetSessionRankingAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeNull();
    }
}
