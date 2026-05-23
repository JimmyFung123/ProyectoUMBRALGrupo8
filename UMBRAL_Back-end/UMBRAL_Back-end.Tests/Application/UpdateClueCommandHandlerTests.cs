namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.UpdateClue;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class UpdateClueCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly UpdateClueCommandHandler _handler;

    public UpdateClueCommandHandlerTests()
    {
        _handler = new UpdateClueCommandHandler(_repositoryMock.Object);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static Mission MissionWithTriviaClue(out Guid stageId, out Guid clueId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Trivia Stage", 1, StageType.Trivia, 100, false, question: "Q?");
        stageId = mission.Stages.First().Id;
        mission.AddClue(stageId, false, 1, "Original hint", null, null, null);
        clueId = mission.Stages.First().Clues.First().Id;
        return mission;
    }

    private static Mission MissionWithTreasureHuntClue(out Guid stageId, out Guid clueId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("TH Stage", 1, StageType.TreasureHunt, 200, false,
            latitude: 10.48, longitude: -66.87, qrCode: "QR-001");
        stageId = mission.Stages.First().Id;
        mission.AddClue(stageId, false, 1, null, 10.48, -66.87, 50.0);
        clueId = mission.Stages.First().Clues.First().Id;
        return mission;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsMissionNotFound()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Mission?)null);

        var result = await _handler.Handle(
            new UpdateClueCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "Hint", null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
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
            new UpdateClueCommand(mission.Id, stageId, Guid.NewGuid(), 1, "Hint", null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.NotFound);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_UpdateTriviaClue_WithValidData_Succeeds()
    {
        var mission = MissionWithTriviaClue(out var stageId, out var clueId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(
            new UpdateClueCommand(mission.Id, stageId, clueId, 1, "Updated hint", null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.First().Clues.First().Content.Should().Be("Updated hint");
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateTreasureHuntClue_WithValidData_Succeeds()
    {
        var mission = MissionWithTreasureHuntClue(out var stageId, out var clueId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(
            new UpdateClueCommand(mission.Id, stageId, clueId, 1, null, 10.99, -67.01, 100.0), default);

        result.IsSuccess.Should().BeTrue();
        var updatedClue = mission.Stages.First().Clues.First();
        updatedClue.Latitude.Should().Be(10.99);
        updatedClue.Longitude.Should().Be(-67.01);
        updatedClue.RadiusMeters.Should().Be(100.0);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }
}
