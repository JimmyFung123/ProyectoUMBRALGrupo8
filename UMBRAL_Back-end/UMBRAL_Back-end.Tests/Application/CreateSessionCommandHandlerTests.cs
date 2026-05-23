namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using MediatR;
using Moq;
using UMBRAL_Back_end.Application.Sessions.Commands.CreateSession;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Sessions;
using UMBRAL_Back_end.Domain.Sessions.Events;
using Xunit;

public class CreateSessionCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _missionRepoMock = new();
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly CreateSessionCommandHandler _handler;

    public CreateSessionCommandHandlerTests()
    {
        _handler = new CreateSessionCommandHandler(
            _missionRepoMock.Object,
            _sessionRepoMock.Object,
            _publisherMock.Object);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Mission ActiveMission()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.Trivia, 100, false,
            question: "Q?",
            options: [("A", true), ("B", false)]);
        mission.Activate();
        return mission;
    }

    private static Mission InactiveMission()
        => Mission.Create("Inactive", "desc", DifficultyLevel.Easy, 30).Value;

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MissionNotFound_ReturnsMissionNotFoundError()
    {
        _missionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Mission?)null);

        var result = await _handler.Handle(new CreateSessionCommand(Guid.NewGuid(), "S1", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
        _publisherMock.Verify(p => p.Publish(It.IsAny<INotification>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_MissionNotActive_ReturnsMissionNotActiveError()
    {
        var mission = InactiveMission();
        _missionRepoMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);

        var result = await _handler.Handle(new CreateSessionCommand(mission.Id, "S1", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.MissionNotActive);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
        _publisherMock.Verify(p => p.Publish(It.IsAny<INotification>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesSessionAndPublishesEvent()
    {
        var mission = ActiveMission();
        _missionRepoMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);

        var result = await _handler.Handle(new CreateSessionCommand(mission.Id, "Round 1", null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        _sessionRepoMock.Verify(r => r.AddAsync(It.IsAny<Session>(), default), Times.Once);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
        _publisherMock.Verify(
            p => p.Publish(
                It.Is<SessionCreatedEvent>(e =>
                    e.MissionId == mission.Id &&
                    e.Name == "Round 1"),
                default),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptySessionName_ReturnsInvalidNameError()
    {
        var mission = ActiveMission();
        _missionRepoMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);

        var result = await _handler.Handle(new CreateSessionCommand(mission.Id, "", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.InvalidName);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommandWithScheduledAt_StoresScheduledAt()
    {
        var mission = ActiveMission();
        var scheduledAt = DateTime.UtcNow.AddDays(3);
        _missionRepoMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);

        Session? capturedSession = null;
        _sessionRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Session>(), default))
            .Callback<Session, CancellationToken>((s, _) => capturedSession = s);

        var result = await _handler.Handle(
            new CreateSessionCommand(mission.Id, "Scheduled Round", scheduledAt), default);

        result.IsSuccess.Should().BeTrue();
        capturedSession.Should().NotBeNull();
        capturedSession!.ScheduledAt.Should().Be(scheduledAt);
    }
}
