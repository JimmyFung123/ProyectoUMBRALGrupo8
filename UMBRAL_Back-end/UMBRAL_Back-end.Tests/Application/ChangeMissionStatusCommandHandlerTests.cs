namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using MediatR;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.ChangeMissionStatus;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;
using Xunit;

public class ChangeMissionStatusCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly ChangeMissionStatusCommandHandler _handler;

    public ChangeMissionStatusCommandHandlerTests()
    {
        _handler = new ChangeMissionStatusCommandHandler(_repositoryMock.Object, _publisherMock.Object);
    }

    [Fact]
    public async Task Handle_ActivateMission_WithNoStages_ReturnsNoStagesError()
    {
        var mission = Mission.Create("Test Mission", "desc", DifficultyLevel.Medium, 60).Value;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(mission.Id, default))
            .ReturnsAsync(mission);

        _repositoryMock
            .Setup(r => r.HasActiveSessionsAsync(mission.Id, default))
            .ReturnsAsync(false);

        var command = new ChangeMissionStatusCommand(mission.Id, Activate: true);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NoStages);

        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Mission>(), default), Times.Never);
        _publisherMock.Verify(p => p.Publish(It.IsAny<INotification>(), default), Times.Never);
    }

    [Fact]
    public async Task Handle_DeactivateMission_WhenHasActiveSessions_ReturnsHasActiveSessionsError()
    {
        var mission = Mission.Create("Test Mission", "desc", DifficultyLevel.Medium, 60).Value;

        _repositoryMock
            .Setup(r => r.GetByIdAsync(mission.Id, default))
            .ReturnsAsync(mission);

        _repositoryMock
            .Setup(r => r.HasActiveSessionsAsync(mission.Id, default))
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

        _repositoryMock
            .Setup(r => r.HasActiveSessionsAsync(mission.Id, default))
            .ReturnsAsync(false);

        var command = new ChangeMissionStatusCommand(mission.Id, Activate: false);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Inactive);

        _repositoryMock.Verify(r => r.UpdateAsync(It.IsAny<Mission>(), default), Times.Once);
        _publisherMock.Verify(
            p => p.Publish(It.IsAny<MissionDeactivatedEvent>(), default),
            Times.Once);
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
