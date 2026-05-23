namespace UMBRAL_Back_end.Tests.Application.Stages;

using FluentAssertions;
using Moq;
using StageService.Application.Stages.Commands.SetAutoRelease;
using StageService.Domain.Stages;
using Xunit;

public class SetAutoReleaseCommandHandlerTests
{
    private readonly Mock<IStageRepository> _stageRepoMock = new();
    private readonly SetAutoReleaseCommandHandler _handler;

    public SetAutoReleaseCommandHandlerTests()
    {
        _handler = new SetAutoReleaseCommandHandler(_stageRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenStageNotFound_ReturnsNotFoundError()
    {
        var stageId = Guid.NewGuid();

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stage?)null);

        var result = await _handler.Handle(new SetAutoReleaseCommand(stageId, TimeMinutes: 5, MaxAttempts: 3), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStageExists_SetsAutoReleaseValuesAndSaves()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Etapa con auto-release", StageType.TreasureHunt, 1, 100).Value;

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        var command = new SetAutoReleaseCommand(stageId, TimeMinutes: 10, MaxAttempts: 2);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        stage.AutoReleaseTimeMinutes.Should().Be(10);
        stage.AutoReleaseMaxAttempts.Should().Be(2);
        _stageRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenNullValues_ClearsAutoRelease()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Etapa", StageType.Trivia, 1, 50).Value;
        stage.SetAutoRelease(5, 3); // Pre-configura valores

        _stageRepoMock
            .Setup(r => r.GetByIdAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stage);

        var result = await _handler.Handle(new SetAutoReleaseCommand(stageId, TimeMinutes: null, MaxAttempts: null), default);

        result.IsSuccess.Should().BeTrue();
        stage.AutoReleaseTimeMinutes.Should().BeNull();
        stage.AutoReleaseMaxAttempts.Should().BeNull();
    }
}
