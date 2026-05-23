namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Queries.GetSessionDetail;
using SessionService.Domain.Sessions;
using Xunit;

public class GetSessionDetailQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamRepository> _teamRepoMock = new();
    private readonly GetSessionDetailQueryHandler _handler;

    public GetSessionDetailQueryHandlerTests()
    {
        _handler = new GetSessionDetailQueryHandler(
            _sessionRepoMock.Object,
            _teamRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSessionDoesNotExist_ReturnsFailureWithNotFoundError()
    {
        var sessionId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new GetSessionDetailQuery(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(SessionErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenSessionExistsWithNoTeams_ReturnsSuccessWithEmptyTeamList()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión de prueba").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());

        var result = await _handler.Handle(new GetSessionDetailQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Sesión de prueba");
        result.Value.Teams.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenSessionHasTeams_ReturnsMappedTeamData()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Ronda Final").Value;

        var team = Team.Create(sessionId, "Equipo Alfa");
        team.SetConnected(true);
        team.UpdateProgress(stageOrder: 2, cluesCurrentStage: 1, totalClues: 4);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { team });

        var result = await _handler.Handle(new GetSessionDetailQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Teams.Should().HaveCount(1);

        var teamDto = result.Value.Teams[0];
        teamDto.Name.Should().Be("Equipo Alfa");
        teamDto.IsConnected.Should().BeTrue();
        teamDto.CurrentStageOrder.Should().Be(2);
        teamDto.CluesReceivedCurrentStage.Should().Be(1);
        teamDto.TotalCluesReceived.Should().Be(4);
    }

    [Fact]
    public async Task Handle_WhenSessionHasMultipleTeams_ReturnsAllTeamsMapped()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Campeonato").Value;

        var team1 = Team.Create(sessionId, "Equipo Beta");
        team1.SetConnected(true);
        team1.UpdateProgress(stageOrder: 3, cluesCurrentStage: 0, totalClues: 2);

        var team2 = Team.Create(sessionId, "Equipo Gamma");
        team2.SetConnected(false);
        team2.UpdateProgress(stageOrder: 1, cluesCurrentStage: 2, totalClues: 2);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team> { team1, team2 });

        var result = await _handler.Handle(new GetSessionDetailQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Teams.Should().HaveCount(2);
        result.Value.Teams.Should().Contain(t => t.Name == "Equipo Beta" && t.IsConnected);
        result.Value.Teams.Should().Contain(t => t.Name == "Equipo Gamma" && !t.IsConnected);
    }

    [Fact]
    public async Task Handle_WhenSessionExists_ReturnsCorrectSessionStatus()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Test Status").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamRepoMock
            .Setup(r => r.GetBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Team>());

        var result = await _handler.Handle(new GetSessionDetailQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
    }
}
