namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using MediatR;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.RemoveStage;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class RemoveStageCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly Mock<IPublisher> _publisherMock = new();
    private readonly RemoveStageCommandHandler _handler;

    public RemoveStageCommandHandlerTests()
    {
        _handler = new RemoveStageCommandHandler(_repositoryMock.Object, _publisherMock.Object);
    }

    // ── Helper ───────────────────────────────────────────────────────────────────

    private static Mission MissionWithStage(out Guid stageId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage 1", 1, StageType.Trivia, 100, false, question: "Q?");
        stageId = mission.Stages.First().Id;
        return mission;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsNotFoundError()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Mission?)null);

        var result = await _handler.Handle(new RemoveStageCommand(Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
        _repositoryMock.Verify(r => r.RemoveStageAsync(It.IsAny<MissionStage>(), default), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStageNotFound_ReturnsNotFoundError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);

        var result = await _handler.Handle(new RemoveStageCommand(mission.Id, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenMissionIsActive_ReturnsMissionLockedError()
    {
        var mission = MissionWithStage(out var stageId);
        mission.Activate();

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(new RemoveStageCommand(mission.Id, stageId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
        _repositoryMock.Verify(r => r.RemoveStageAsync(It.IsAny<MissionStage>(), default), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenHasActiveSessions_ReturnsMissionLockedError()
    {
        var mission = MissionWithStage(out var stageId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(true);

        var result = await _handler.Handle(new RemoveStageCommand(mission.Id, stageId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidData_RemovesStageAndSaves()
    {
        var mission = MissionWithStage(out var stageId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(new RemoveStageCommand(mission.Id, stageId), default);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.Should().BeEmpty();
        _repositoryMock.Verify(r => r.RemoveStageAsync(It.IsAny<MissionStage>(), default), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }
}
