namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.AddStageToMission;
using UMBRAL_Back_end.Application.Missions.Commands.UpdateStage;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class UpdateStageCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly UpdateStageCommandHandler _handler;

    public UpdateStageCommandHandlerTests()
    {
        _handler = new UpdateStageCommandHandler(_repositoryMock.Object);
    }

    // ── Helper ───────────────────────────────────────────────────────────────────

    private static Mission MissionWithTriviaStage(out Guid stageId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage 1", 1, StageType.Trivia, 100, false, question: "Q?",
            options: [("A", false), ("B", true)]);
        stageId = mission.Stages.First().Id;
        return mission;
    }

    private static Mission MissionWithTreasureStage(out Guid stageId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Treasure", 1, StageType.TreasureHunt, 200, false,
            latitude: 10.5, longitude: -66.9, qrCode: "QR-001");
        stageId = mission.Stages.First().Id;
        return mission;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsNotFoundError()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Mission?)null);

        var command = new UpdateStageCommand(Guid.NewGuid(), Guid.NewGuid(), "T", 1, 100, null, null, null, null, null);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenStageNotFound_ReturnsNotFoundError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);

        var command = new UpdateStageCommand(mission.Id, Guid.NewGuid(), "T", 1, 100, null, null, null, null, null);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenMissionIsActive_ReturnsMissionLockedError()
    {
        var mission = MissionWithTriviaStage(out var stageId);
        mission.Activate();

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var command = new UpdateStageCommand(mission.Id, stageId, "New Title", 1, 100, "Q?", null, null, null, null);

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_UpdateTriviaStage_WithValidData_Succeeds()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var command = new UpdateStageCommand(
            mission.Id, stageId,
            Title: "Updated Title", Order: 1, BaseScore: 200,
            Question: "Updated Q?",
            Options: [new TriviaOptionInput("X", false), new TriviaOptionInput("Y", true)],
            Latitude: null, Longitude: null, QrCode: null);

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _repositoryMock.Verify(r => r.ReplaceStageOptionsAsync(stageId, It.IsAny<IEnumerable<(string, bool)>>(), default), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateTreasureHuntStage_WithDuplicateQr_ReturnsDuplicateQrCodeError()
    {
        var mission = MissionWithTreasureStage(out var stageId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasDuplicateQrCodeAsync("QR-TAKEN", stageId, default)).ReturnsAsync(true);

        var command = new UpdateStageCommand(
            mission.Id, stageId,
            "Treasure", 1, 200, null, null, 10.5, -66.9, "QR-TAKEN");

        var result = await _handler.Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.DuplicateQrCode);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_UpdateTreasureHuntStage_WithValidData_Succeeds()
    {
        var mission = MissionWithTreasureStage(out var stageId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);
        _repositoryMock.Setup(r => r.HasDuplicateQrCodeAsync("QR-001", stageId, default)).ReturnsAsync(false);

        var command = new UpdateStageCommand(
            mission.Id, stageId,
            "Treasure Updated", 1, 300, null, null, 11.0, -67.0, "QR-001");

        var result = await _handler.Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        // No options to replace for TreasureHunt
        _repositoryMock.Verify(r => r.ReplaceStageOptionsAsync(It.IsAny<Guid>(), It.IsAny<IEnumerable<(string, bool)>>(), default), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }
}
