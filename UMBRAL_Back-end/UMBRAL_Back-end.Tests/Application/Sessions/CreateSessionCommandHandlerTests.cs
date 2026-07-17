namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application;
using SessionService.Application.Sessions.Commands.CreateSession;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using Xunit;

public class CreateSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IMissionLookupRepository> _missionLookupMock = new();
    private readonly CreateSessionCommandHandler _handler;

    public CreateSessionCommandHandlerTests()
    {
        _handler = new CreateSessionCommandHandler(
            _sessionRepoMock.Object,
            _missionLookupMock.Object);
    }

    [Fact]
    public async Task Handle_WhenMissionLookupNotFound_ReturnsMissionNotFoundError()
    {
        var missionId = Guid.NewGuid();

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionLookup?)null);

        var result = await _handler.Handle(new CreateSessionCommand(missionId, "Sesión válida", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("MissionLookup.NotFound");
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionIsNotActive_ReturnsMissionNotActiveError()
    {
        var missionId = Guid.NewGuid();
        var inactiveMission = MissionLookup.Create(missionId, "Misión inactiva", "Inactive");

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(inactiveMission);

        var result = await _handler.Handle(new CreateSessionCommand(missionId, "Sesión válida", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.MissionNotActive);
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNameIsEmpty_ReturnsInvalidNameError()
    {
        var missionId = Guid.NewGuid();
        var activeMission = MissionLookup.Create(missionId, "Misión activa", "Active");

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMission);

        var result = await _handler.Handle(new CreateSessionCommand(missionId, "   ", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.InvalidName);
    }

    [Fact]
    public async Task Handle_WhenValid_CreatesSessionAndReturnsNewId()
    {
        var missionId = Guid.NewGuid();
        var activeMission = MissionLookup.Create(missionId, "Misión activa", "Active");

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMission);

        var result = await _handler.Handle(new CreateSessionCommand(missionId, "Ronda 1", null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()), Times.Once);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenValid_RaisesSessionCreatedDomainEventWithOperator()
    {
        // HU-26: the creation event must carry the operator that triggered it,
        // so the translation handler can build the audit log entry from it.
        var missionId = Guid.NewGuid();
        var activeMission = MissionLookup.Create(missionId, "Misión activa", "Active");

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMission);

        Session? capturedSession = null;
        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => capturedSession = s);

        var result = await _handler.Handle(
            new CreateSessionCommand(missionId, "Ronda 1", null, "Prof. Ortega"),
            default);

        result.IsSuccess.Should().BeTrue();
        capturedSession!.DomainEvents.Should().ContainSingle();
        var raised = capturedSession.DomainEvents.Single().Should().BeOfType<SessionCreatedDomainEvent>().Subject;
        raised.OperatorName.Should().Be("Prof. Ortega");
        raised.Name.Should().Be("Ronda 1");
    }

    [Fact]
    public async Task Handle_WhenScheduledAtProvided_CreatesSessionWithUtcDate()
    {
        var missionId = Guid.NewGuid();
        var activeMission = MissionLookup.Create(missionId, "Misión activa", "Active");
        var scheduledAt = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Unspecified);

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMission);

        Session? capturedSession = null;
        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => capturedSession = s);

        var result = await _handler.Handle(new CreateSessionCommand(missionId, "Ronda programada", scheduledAt), default);

        result.IsSuccess.Should().BeTrue();
        capturedSession!.ScheduledAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task Handle_WhenOperatorIdProvided_StampsSessionWithCreatedByOperatorId()
    {
        // RB-10: the created session must record its owner for later ownership checks.
        var missionId = Guid.NewGuid();
        var activeMission = MissionLookup.Create(missionId, "Misión activa", "Active");

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMission);

        Session? capturedSession = null;
        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Session>(), It.IsAny<CancellationToken>()))
            .Callback<Session, CancellationToken>((s, _) => capturedSession = s);

        var result = await _handler.Handle(
            new CreateSessionCommand(missionId, "Ronda 1", null, "Prof. Ortega", "keycloak-sub-abc"),
            default);

        result.IsSuccess.Should().BeTrue();
        capturedSession!.CreatedByOperatorId.Should().Be("keycloak-sub-abc");
    }
}
