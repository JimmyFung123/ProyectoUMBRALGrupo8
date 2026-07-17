namespace UMBRAL_Back_end.Tests.Application.Stages;

using FluentAssertions;
using Moq;
using StageService.Application;
using StageService.Application.Stages.Commands.RemoveStage;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;
using UMBRAL.Contracts.Events;
using Xunit;

public class RemoveStageCommandHandlerTests
{
    private readonly Mock<IStageRepository> _stageRepoMock = new();
    private readonly Mock<IMissionLookupRepository> _missionLookupMock = new();
    private readonly Mock<IIntegrationEventBus> _busMock = new();
    private readonly RemoveStageCommandHandler _handler;

    public RemoveStageCommandHandlerTests()
    {
        _handler = new RemoveStageCommandHandler(
            _stageRepoMock.Object,
            _missionLookupMock.Object,
            _busMock.Object);
    }

    [Fact]
    public async Task Handle_WhenMissionIsActive_ReturnsMissionIsActiveError()
    {
        var missionId = Guid.NewGuid();
        var activeMission = MissionLookup.Create(missionId, "Active Mission", "Active");

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeMission);

        var command = new RemoveStageCommand(Guid.NewGuid(), missionId);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionIsActive);
        _stageRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Stage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStageNotFound_ReturnsNotFoundError()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionLookup?)null); // Inactive by fallback

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stage?)null);

        var command = new RemoveStageCommand(stageId, missionId);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenStageExists_DeletesAndPublishesEvent()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Etapa a eliminar", StageType.Trivia, 1, 50).Value;

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionLookup?)null);

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        var command = new RemoveStageCommand(stageId, missionId);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        _stageRepoMock.Verify(r => r.DeleteAsync(stage, It.IsAny<CancellationToken>()), Times.Once);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _busMock.Verify(
            b => b.PublishAsync(It.IsAny<StageRemovedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
