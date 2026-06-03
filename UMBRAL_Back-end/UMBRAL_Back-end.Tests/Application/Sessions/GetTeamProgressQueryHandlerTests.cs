namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Queries.GetTeamProgress;
using TeamService.Domain.Teams;
using Xunit;

public class GetTeamProgressQueryHandlerTests
{
    private readonly Mock<ITeamRepository> _teamRepoMock = new();
    private readonly GetTeamProgressQueryHandler _handler;

    public GetTeamProgressQueryHandlerTests()
    {
        _handler = new GetTeamProgressQueryHandler(_teamRepoMock.Object);
    }

    // ── Sin equipos ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNoTeams_ReturnsEmptyList()
    {
        var sessionId = Guid.NewGuid();

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());

        var result = await _handler.Handle(new GetTeamProgressQuery(sessionId), default);

        result.Should().BeEmpty();
    }

    // ── Ordenamiento por score (ranking) ──────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamsHaveDifferentScores_ReturnsSortedByScoreDescending()
    {
        var sessionId = Guid.NewGuid();

        var teamA = Team.Create(sessionId, TeamName.Create("Equipo Alfa").Value);
        teamA.UpdateScore(300);
        teamA.UpdateProgress(stageOrder: 3, cluesCurrentStage: 0, totalClues: 0);

        var teamB = Team.Create(sessionId, TeamName.Create("Equipo Beta").Value);
        teamB.UpdateScore(150);
        teamB.UpdateProgress(stageOrder: 2, cluesCurrentStage: 1, totalClues: 0);

        var teamC = Team.Create(sessionId, TeamName.Create("Equipo Gamma").Value);
        teamC.UpdateScore(450);
        teamC.UpdateProgress(stageOrder: 4, cluesCurrentStage: 0, totalClues: 0);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { teamA, teamB, teamC });

        var result = await _handler.Handle(new GetTeamProgressQuery(sessionId), default);

        result.Select(t => t.Name).Should().Equal("Equipo Gamma", "Equipo Alfa", "Equipo Beta");
        result.Select(t => t.Score).Should().Equal(450, 300, 150);
    }

    // ── Asignación de rankings (posiciones) ───────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamsHaveSameScore_AssignsSameRank()
    {
        var sessionId = Guid.NewGuid();

        var teamA = Team.Create(sessionId, TeamName.Create("Equipo Alfa").Value);
        teamA.UpdateScore(200);

        var teamB = Team.Create(sessionId, TeamName.Create("Equipo Beta").Value);
        teamB.UpdateScore(200);

        var teamC = Team.Create(sessionId, TeamName.Create("Equipo Gamma").Value);
        teamC.UpdateScore(100);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { teamA, teamB, teamC });

        var result = await _handler.Handle(new GetTeamProgressQuery(sessionId), default);

        // Both tied teams share rank 1; Gamma drops to rank 3 (dense rank)
        var ranked = result.ToList();
        ranked.Where(t => t.Score == 200).Should().AllSatisfy(t => t.Rank.Should().Be(1));
        ranked.Single(t => t.Name == "Equipo Gamma").Rank.Should().Be(3);
    }

    // ── Mapeo de campos ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamHasProgress_MapsAllFieldsCorrectly()
    {
        var sessionId = Guid.NewGuid();

        var team = Team.Create(sessionId, TeamName.Create("Equipo Delta").Value);
        team.SetConnected(true);
        team.UpdateScore(500);
        team.UpdateProgress(stageOrder: 2, cluesCurrentStage: 1, totalClues: 3);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { team });

        var result = await _handler.Handle(new GetTeamProgressQuery(sessionId), default);

        var dto = result.Single();
        dto.Name.Should().Be("Equipo Delta");
        dto.IsConnected.Should().BeTrue();
        dto.Score.Should().Be(500);
        dto.CurrentStageOrder.Should().Be(2);
        dto.Rank.Should().Be(1);
    }

    // ── Un solo equipo ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOnlyOneTeam_ReturnsRankOne()
    {
        var sessionId = Guid.NewGuid();
        var team = Team.Create(sessionId, TeamName.Create("Equipo Solitario").Value);
        team.UpdateScore(100);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { team });

        var result = await _handler.Handle(new GetTeamProgressQuery(sessionId), default);

        result.Single().Rank.Should().Be(1);
    }

    // ── Desempate alfabético ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamsTied_OrdersAlphabeticallyWithinSameRank()
    {
        var sessionId = Guid.NewGuid();

        var teamZ = Team.Create(sessionId, TeamName.Create("Zorros").Value);
        teamZ.UpdateScore(200);

        var teamA = Team.Create(sessionId, TeamName.Create("Águilas").Value);
        teamA.UpdateScore(200);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { teamZ, teamA });

        var result = await _handler.Handle(new GetTeamProgressQuery(sessionId), default);

        // Both rank 1, alphabetical order within the tie
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.Rank.Should().Be(1));
        result.First().Name.Should().Be("Águilas");
        result.Last().Name.Should().Be("Zorros");
    }

    // ── Ranking con salto (dense) ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenThreeWayTieFollowedByLower_RankJumpsToFour()
    {
        var sessionId = Guid.NewGuid();

        var teamA = Team.Create(sessionId, TeamName.Create("Equipo A").Value);
        teamA.UpdateScore(300);

        var teamB = Team.Create(sessionId, TeamName.Create("Equipo B").Value);
        teamB.UpdateScore(300);

        var teamC = Team.Create(sessionId, TeamName.Create("Equipo C").Value);
        teamC.UpdateScore(300);

        var teamD = Team.Create(sessionId, TeamName.Create("Equipo D").Value);
        teamD.UpdateScore(100);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { teamA, teamB, teamC, teamD });

        var result = await _handler.Handle(new GetTeamProgressQuery(sessionId), default);

        result.Where(t => t.Score == 300).Should().AllSatisfy(t => t.Rank.Should().Be(1));
        result.Single(t => t.Name == "Equipo D").Rank.Should().Be(4);
    }
}
