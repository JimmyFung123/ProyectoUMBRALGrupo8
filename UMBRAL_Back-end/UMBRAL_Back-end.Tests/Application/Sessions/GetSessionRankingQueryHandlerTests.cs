namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Queries.GetSessionRanking;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// HU-21: SessionService proxy handler — sits between the UI and TeamService's
/// optimized read model so participants/operators only ever talk to SessionService.
/// </summary>
public class GetSessionRankingQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly GetSessionRankingQueryHandler _handler;

    public GetSessionRankingQueryHandlerTests()
    {
        _handler = new GetSessionRankingQueryHandler(
            _sessionRepoMock.Object,
            _teamClientMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new GetSessionRankingQuery(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenTeamServiceReturnsNull_ReturnsEmptyRankingButSuccess()
    {
        // Defensive path: if TeamService is unreachable, we still return a valid
        // (empty) ranking so the client can show "desconectado / sincronizando"
        // and keep its last-known data on screen (flujo alterno HU-21).
        var session = Session.Create(Guid.NewGuid(), "Sesión Test").Value;
        session.Start();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _teamClientMock
            .Setup(c => c.GetSessionRankingAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SessionRankingSnapshot?)null);

        var result = await _handler.Handle(new GetSessionRankingQuery(session.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Teams.Should().BeEmpty();
        result.Value.SessionStatus.Should().Be("InProgress");
    }

    [Fact]
    public async Task Handle_WhenSessionInProgress_PassesThroughRankingFromTeamService()
    {
        var session = Session.Create(Guid.NewGuid(), "En curso").Value;
        session.Start();

        var team1 = new SessionRankingTeamItem(
            Guid.NewGuid(), "Equipo Alfa", Score: 300, Rank: 1,
            CurrentStageOrder: 3, IsConnected: true,
            LastStageCompletedAt: new DateTime(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc));
        var team2 = new SessionRankingTeamItem(
            Guid.NewGuid(), "Equipo Beta", Score: 100, Rank: 2,
            CurrentStageOrder: 1, IsConnected: false,
            LastStageCompletedAt: null);

        var snapshot = new SessionRankingSnapshot(
            session.Id, DateTime.UtcNow, new[] { team1, team2 });

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _teamClientMock
            .Setup(c => c.GetSessionRankingAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var result = await _handler.Handle(new GetSessionRankingQuery(session.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Teams.Should().HaveCount(2);
        result.Value.Teams.First().Name.Should().Be("Equipo Alfa");
        result.Value.Teams.First().Score.Should().Be(300);
        result.Value.Teams.First().Rank.Should().Be(1);
        result.Value.Teams.Last().LastStageCompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenSessionPending_ResetsAllScoresToZero()
    {
        // Flujo alterno HU-21: con la sesión en "preparación" todos los equipos
        // deben mostrarse con 0 puntos y sin tiempo de resolución.
        var session = Session.Create(Guid.NewGuid(), "En preparación").Value;
        // session stays Pending

        var team = new SessionRankingTeamItem(
            Guid.NewGuid(), "Equipo X", Score: 999, Rank: 1,
            CurrentStageOrder: 0, IsConnected: true,
            LastStageCompletedAt: DateTime.UtcNow);
        var snapshot = new SessionRankingSnapshot(session.Id, DateTime.UtcNow, new[] { team });

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _teamClientMock
            .Setup(c => c.GetSessionRankingAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        var result = await _handler.Handle(new GetSessionRankingQuery(session.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.SessionStatus.Should().Be("Pending");
        var dto = result.Value.Teams.Single();
        dto.Score.Should().Be(0);
        dto.LastStageCompletedAt.Should().BeNull();
    }
}
