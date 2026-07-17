namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using Moq;
using UMBRAL_Back_end.Application;
using UMBRAL_Back_end.Application.Missions;
using UMBRAL_Back_end.Application.Missions.Commands.ChangeMissionStatus;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;
using Xunit;

public class ChangeMissionStatusCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly Mock<IStageCountLookupRepository> _stageCountLookupMock = new();
    private readonly Mock<ISessionServiceClient> _sessionClientMock = new();
    private readonly ChangeMissionStatusCommandHandler _handler;

    public ChangeMissionStatusCommandHandlerTests()
    {
        _handler = new ChangeMissionStatusCommandHandler(
            _repositoryMock.Object,
            _stageCountLookupMock.Object,
            _sessionClientMock.Object);
    }

    [Fact]
    public async Task Handle_ActivateMission_Succeeds()
    {
        var mission = Mission.Create("Test Mission", "desc", DifficultyLevel.Medium, 60).Value;
        var stageCount = StageCountLookup.Create(mission.Id);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(mission.Id, default))
            .ReturnsAsync(mission);

        _stageCountLookupMock
            .Setup(r => r.GetByMissionIdAsync(mission.Id, default))
            .ReturnsAsync(stageCount);

        var command = new ChangeMissionStatusCommand(mission.Id, Activate: true);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Active);

        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Mission>(), default), Times.Once);
        mission.DomainEvents.OfType<MissionActivatedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_DeactivateMission_WhenHasActiveSessions_ReturnsHasActiveSessionsError()
    {
        var mission = Mission.Create("Test Mission", "desc", DifficultyLevel.Medium, 60).Value;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(mission.Id, default))
            .ReturnsAsync(mission);

        _sessionClientMock
            .Setup(c => c.HasActiveSessionsAsync(mission.Id, default))
            .ReturnsAsync(true);

        var command = new ChangeMissionStatusCommand(mission.Id, Activate: false);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.HasActiveSessions);
    }

    [Fact]
    public async Task Handle_DeactivateMission_WhenNoActiveSessions_Succeeds()
    {
        var mission = Mission.Create("Test Mission", "desc", DifficultyLevel.Medium, 60).Value;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(mission.Id, default))
            .ReturnsAsync(mission);

        _sessionClientMock
            .Setup(c => c.HasActiveSessionsAsync(mission.Id, default))
            .ReturnsAsync(false);

        var command = new ChangeMissionStatusCommand(mission.Id, Activate: false);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Inactive);

        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Mission>(), default), Times.Once);
        mission.DomainEvents.OfType<MissionDeactivatedDomainEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_DeactivateMission_WhenSessionServiceUnreachable_BlocksDeactivation()
    {
        // Fail-safe: if SessionService is down we assume there MIGHT be active sessions
        // and block the deactivation to avoid orphaning them (SessionServiceClient returns true on error).
        var mission = Mission.Create("Test Mission", "desc", DifficultyLevel.Medium, 60).Value;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(mission.Id, default))
            .ReturnsAsync(mission);

        _sessionClientMock
            .Setup(c => c.HasActiveSessionsAsync(mission.Id, default))
            .ReturnsAsync(true); // simulates unreachable service returning fail-safe true

        var command = new ChangeMissionStatusCommand(mission.Id, Activate: false);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.HasActiveSessions);
        _repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsNotFoundError()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync((Mission?)null);

        var command = new ChangeMissionStatusCommand(Guid.NewGuid(), Activate: true);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
    }
}
