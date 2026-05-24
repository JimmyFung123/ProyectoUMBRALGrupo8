namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Queries.GetSessionDetail;
using SessionService.Domain.Sessions;
using Xunit;

public class GetSessionDetailQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly GetSessionDetailQueryHandler _handler;

    public GetSessionDetailQueryHandlerTests()
    {
        _handler = new GetSessionDetailQueryHandler(_sessionRepoMock.Object);
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
    public async Task Handle_WhenSessionExists_ReturnsSuccessWithSessionData()
    {
        var missionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var session = Session.Create(missionId, "Sesión de prueba").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new GetSessionDetailQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Sesión de prueba");
        result.Value.MissionId.Should().Be(missionId);
    }

    [Fact]
    public async Task Handle_WhenSessionExists_ReturnsCorrectSessionStatus()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Test Status").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new GetSessionDetailQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Pending");
    }
}
