namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Queries.GetSessionRanking;
using TeamService.Domain.Teams;
using Xunit;

/// <summary>
/// HU-21: ranking lectura optimizada.
/// Sort contract: Score desc, then LastStageCompletedAt asc (nulls last), then Name.
/// Rank uses dense ranking — tied teams share the same rank, the next distinct score
/// gets Rank = i + 1.
/// </summary>
public class GetSessionRankingQueryHandlerTests
{
    private readonly Mock<ITeamRepository> _teamRepoMock = new();
    private readonly GetSessionRankingQueryHandler _handler;

    public GetSessionRankingQueryHandlerTests()
    {
        _handler = new GetSessionRankingQueryHandler(_teamRepoMock.Object);
    }

    // ── Sin equipos ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoTeams_ReturnsEmptySnapshot()
    {
        var sessionId = Guid.NewGuid();
        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        result.SessionId.Should().Be(sessionId);
        result.Teams.Should().BeEmpty();
    }

    // ── Orden por score ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenDifferentScores_OrdersByScoreDescending()
    {
        var sessionId = Guid.NewGuid();
        var teamLow  = MakeTeam(sessionId, "Bajo",  score: 50);
        var teamMid  = MakeTeam(sessionId, "Medio", score: 200);
        var teamTop  = MakeTeam(sessionId, "Alto",  score: 350);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { teamLow, teamMid, teamTop });

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        result.Teams.Select(t => t.Name).Should().Equal("Alto", "Medio", "Bajo");
        result.Teams.Select(t => t.Rank).Should().Equal(1, 2, 3);
    }

    // ── Desempate por tiempo de resolución (criterio HU-21) ───────────────────

    [Fact]
    public async Task Handle_WhenScoresTie_UsesLastStageCompletedAtAsTieBreaker()
    {
        var sessionId = Guid.NewGuid();
        var earlier = new DateTime(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc);
        var later   = new DateTime(2026, 5, 25, 10, 5, 0, DateTimeKind.Utc);

        // Both teams ended with 200 points, but Equipo A resolved its last stage first.
        var teamLate  = MakeTeam(sessionId, "Equipo B", score: 200, lastStageCompletedAt: later);
        var teamEarly = MakeTeam(sessionId, "Equipo A", score: 200, lastStageCompletedAt: earlier);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { teamLate, teamEarly });

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        // Earlier resolution wins the tie — appears first, but both share Rank 1 (dense rank).
        result.Teams.Select(t => t.Name).Should().Equal("Equipo A", "Equipo B");
        result.Teams.Should().AllSatisfy(t => t.Rank.Should().Be(1));
    }

    [Fact]
    public async Task Handle_WhenScoresTieAndOneTeamHasNoResolutionTime_PutsTeamWithoutTimeLast()
    {
        var sessionId = Guid.NewGuid();
        var teamWithTime = MakeTeam(sessionId, "Con tiempo", score: 100,
            lastStageCompletedAt: new DateTime(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc));
        var teamNoTime   = MakeTeam(sessionId, "Sin tiempo", score: 100,
            lastStageCompletedAt: null);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { teamNoTime, teamWithTime });

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        result.Teams.First().Name.Should().Be("Con tiempo");
        result.Teams.Last().Name.Should().Be("Sin tiempo");
    }

    // ── Score gana sobre tiempo de resolución ─────────────────────────────────

    [Fact]
    public async Task Handle_WhenScoresDiffer_ScoreTakesPriorityOverResolutionTime()
    {
        var sessionId = Guid.NewGuid();
        // Lower score but earlier resolution — must still rank below the higher-score team.
        var fastButLower = MakeTeam(sessionId, "Veloz", score: 100,
            lastStageCompletedAt: new DateTime(2026, 5, 25, 9, 0, 0, DateTimeKind.Utc));
        var slowButHigher = MakeTeam(sessionId, "Líder", score: 500,
            lastStageCompletedAt: new DateTime(2026, 5, 25, 11, 0, 0, DateTimeKind.Utc));

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { fastButLower, slowButHigher });

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        result.Teams.First().Name.Should().Be("Líder");
        result.Teams.First().Rank.Should().Be(1);
    }

    // ── Snapshot timestamp ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_PopulatesGeneratedAt()
    {
        var sessionId = Guid.NewGuid();
        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());

        var before = DateTime.UtcNow;
        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);
        var after  = DateTime.UtcNow;

        result.GeneratedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    // ── Mapeo de campos ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamHasProgress_MapsAllFields()
    {
        var sessionId = Guid.NewGuid();
        var team = Team.Create(sessionId, "Equipo Delta");
        team.SetConnected(true);
        team.AnswerTrivia(isCorrect: true, scoreChange: 150, nextStageOrder: 2);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { team });

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        var dto = result.Teams.Single();
        dto.Name.Should().Be("Equipo Delta");
        dto.Score.Should().Be(150);
        dto.Rank.Should().Be(1);
        dto.CurrentStageOrder.Should().Be(2);
        dto.IsConnected.Should().BeTrue();
        dto.LastStageCompletedAt.Should().NotBeNull();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Team MakeTeam(
        Guid sessionId,
        string name,
        int score,
        DateTime? lastStageCompletedAt = null)
    {
        var team = Team.Create(sessionId, name);
        team.UpdateScore(score);
        if (lastStageCompletedAt.HasValue)
        {
            // Tests need a deterministic resolution time — the domain method (AnswerTrivia)
            // always stamps DateTime.UtcNow, so we use the private setter via reflection.
            // Acceptable test seam: the production code keeps its public surface clean.
            var prop = typeof(Team).GetProperty(nameof(Team.LastStageCompletedAt))!;
            prop.GetSetMethod(nonPublic: true)!
                .Invoke(team, new object?[] { lastStageCompletedAt.Value });
        }
        return team;
    }
}
