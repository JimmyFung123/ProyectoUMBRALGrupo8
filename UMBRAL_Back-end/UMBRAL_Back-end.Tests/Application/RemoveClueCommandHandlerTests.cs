namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.RemoveClue;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class RemoveClueCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly RemoveClueCommandHandler _handler;

    public RemoveClueCommandHandlerTests()
    {
        _handler = new RemoveClueCommandHandler(_repositoryMock.Object);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static Mission MissionWithClue(out Guid stageId, out Guid clueId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Trivia Stage", 1, StageType.Trivia, 100, false, question: "Q?");
        stageId = mission.Stages.First().Id;
        mission.AddClue(stageId, false, 1, "Hint to remove", null, null, null);
        clueId = mission.Stages.First().Clues.First().Id;
        return mission;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsMissionNotFound()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Mission?)null);

        var result = await _handler.Handle(
            new RemoveClueCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
        _repositoryMock.Verify(r => r.RemoveClueAsync(It.IsAny<Clue>(), default), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenClueNotFound_ReturnsClueNotFound()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.Trivia, 100, false, question: "Q?");
        var stageId = mission.Stages.First().Id;

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(
            new RemoveClueCommand(mission.Id, stageId, Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.NotFound);
        _repositoryMock.Verify(r => r.RemoveClueAsync(It.IsAny<Clue>(), default), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WithValidClue_RemovesClueAndSaves()
    {
        var mission = MissionWithClue(out var stageId, out var clueId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(
            new RemoveClueCommand(mission.Id, stageId, clueId), default);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.First().Clues.Should().BeEmpty();
        _repositoryMock.Verify(r => r.RemoveClueAsync(It.IsAny<Clue>(), default), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }
}
