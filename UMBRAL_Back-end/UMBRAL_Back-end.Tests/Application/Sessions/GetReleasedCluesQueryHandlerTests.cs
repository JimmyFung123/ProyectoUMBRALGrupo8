namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Queries.GetReleasedClues;
using SessionService.Domain.Sessions;
using Xunit;

public class GetReleasedCluesQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IStageServiceClient> _stageClientMock = new();
    private readonly Mock<IClueServiceClient> _clueClientMock = new();
    private readonly GetReleasedCluesQueryHandler _handler;

    public GetReleasedCluesQueryHandlerTests()
    {
        _handler = new GetReleasedCluesQueryHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _stageClientMock.Object,
            _clueClientMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSessionDoesNotExist_ReturnsNotFound()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new GetReleasedCluesQuery(sessionId, teamId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(SessionErrors.NotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenTeamNotInSession_ReturnsTeamNotFound()
    {
        var missionId = Guid.NewGuid();
        var session = Session.Create(missionId, "S1").Value;
        var teamId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new GetReleasedCluesQuery(session.Id, teamId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(SessionErrors.TeamNotFound.Code);
    }

    [Fact]
    public async Task Handle_WhenTeamHasNotStarted_ReturnsEmptyClueList()
    {
        var session = Session.Create(Guid.NewGuid(), "S1").Value;
        var teamId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamProgressItem>
            {
                new(teamId, "Equipo A", CurrentStageOrder: 0, CluesReceivedCurrentStage: 0,
                    ClueTimerResetAt: null, LastClueWasAutomatic: false),
            });

        var result = await _handler.Handle(new GetReleasedCluesQuery(session.Id, teamId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Clues.Should().BeEmpty();
        result.Value.CluesReceived.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTeamHasReceivedTwoClues_ReturnsFirstTwoInOrder()
    {
        var missionId = Guid.NewGuid();
        var session = Session.Create(missionId, "S1").Value;
        var teamId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamProgressItem>
            {
                new(teamId, "Equipo A", CurrentStageOrder: 1, CluesReceivedCurrentStage: 2,
                    ClueTimerResetAt: null, LastClueWasAutomatic: false),
            });

        _stageClientMock
            .Setup(s => s.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stageId, 1)]);

        _stageClientMock
            .Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(
                stageId, "Etapa 1", "Trivia", 1, 100, "¿Pregunta?", [], null, null, null));

        // Provide three clues; only the first two should be returned.
        _clueClientMock
            .Setup(c => c.GetCluesByStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ClueInfo(Guid.NewGuid(), 1, "Pista 1", null, null, null, 5),
                new ClueInfo(Guid.NewGuid(), 2, "Pista 2", null, null, null, 5),
                new ClueInfo(Guid.NewGuid(), 3, "Pista 3", null, null, null, 5),
            ]);

        var result = await _handler.Handle(new GetReleasedCluesQuery(session.Id, teamId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CluesReceived.Should().Be(2);
        result.Value.TotalCluesForStage.Should().Be(3);
        result.Value.Clues.Should().HaveCount(2);
        result.Value.Clues[0].Content.Should().Be("Pista 1");
        result.Value.Clues[1].Content.Should().Be("Pista 2");
        result.Value.StageType.Should().Be("Trivia");
    }

    [Fact]
    public async Task Handle_WhenCluesReceivedExceedsConfigured_ClampsToAvailableClues()
    {
        var missionId = Guid.NewGuid();
        var session = Session.Create(missionId, "S1").Value;
        var teamId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _teamClientMock
            .Setup(t => t.GetTeamProgressAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TeamProgressItem>
            {
                new(teamId, "Equipo A", CurrentStageOrder: 1, CluesReceivedCurrentStage: 10,
                    ClueTimerResetAt: null, LastClueWasAutomatic: false),
            });

        _stageClientMock
            .Setup(s => s.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stageId, 1)]);

        _stageClientMock
            .Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(
                stageId, "Etapa 1", "TreasureHunt", 1, 100, null, [], 10.0, -66.0, "QR-1"));

        _clueClientMock
            .Setup(c => c.GetCluesByStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ClueInfo(Guid.NewGuid(), 1, null, 10.0, -66.0, 50, null)]);

        var result = await _handler.Handle(new GetReleasedCluesQuery(session.Id, teamId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.CluesReceived.Should().Be(1);
        result.Value.Clues.Should().HaveCount(1);
        result.Value.StageType.Should().Be("TreasureHunt");
    }
}
