namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Queries.GetSessions;
using SessionService.Domain.Sessions;
using Xunit;

public class GetSessionsQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _repoMock = new();
    private readonly GetSessionsQueryHandler _handler;

    public GetSessionsQueryHandlerTests()
        => _handler = new GetSessionsQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_MapsSessionsToDtoList()
    {
        var missionId = Guid.NewGuid();
        var sessions = new List<Session>
        {
            Session.Create(missionId, "Sesión A").Value,
            Session.Create(missionId, "Sesión B").Value,
        };
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<SessionStatus?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(sessions);

        var result = await _handler.Handle(new GetSessionsQuery(), default);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Sesión A");
        result[1].MissionId.Should().Be(missionId);
    }

    [Fact]
    public async Task Handle_WhenFiltersProvided_ForwardsMissionIdAndParsedStatus()
    {
        var missionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<SessionStatus?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Session>());

        await _handler.Handle(new GetSessionsQuery(missionId, "InProgress"), default);

        _repoMock.Verify(
            r => r.GetAllAsync(missionId, SessionStatus.InProgress, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStatusInvalid_PassesNullStatus()
    {
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<Guid?>(), It.IsAny<SessionStatus?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Session>());

        await _handler.Handle(new GetSessionsQuery(null, "NoExiste"), default);

        _repoMock.Verify(
            r => r.GetAllAsync(null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
