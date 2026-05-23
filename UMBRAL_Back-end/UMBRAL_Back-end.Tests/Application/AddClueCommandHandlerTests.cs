namespace UMBRAL_Back_end.Tests.Application;

using FluentAssertions;
using Moq;
using UMBRAL_Back_end.Application.Missions.Commands.AddClue;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class AddClueCommandHandlerTests
{
    private readonly Mock<IMissionRepository> _repositoryMock = new();
    private readonly AddClueCommandHandler _handler;

    public AddClueCommandHandlerTests()
    {
        _handler = new AddClueCommandHandler(_repositoryMock.Object);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static Mission MissionWithTriviaStage(out Guid stageId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Trivia Stage", 1, StageType.Trivia, 100, false, question: "Q?");
        stageId = mission.Stages.First().Id;
        return mission;
    }

    private static Mission MissionWithTreasureHuntStage(out Guid stageId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("TH Stage", 1, StageType.TreasureHunt, 200, false,
            latitude: 10.48, longitude: -66.87, qrCode: "QR-001");
        stageId = mission.Stages.First().Id;
        return mission;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsMissionNotFound()
    {
        _repositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Mission?)null);

        var result = await _handler.Handle(
            new AddClueCommand(Guid.NewGuid(), Guid.NewGuid(), 1, "Hint", null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
        _repositoryMock.Verify(r => r.AddClueAsync(It.IsAny<Clue>(), default), Times.Never);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_AddTriviaClue_WithoutContent_ReturnsContentRequired()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(
            new AddClueCommand(mission.Id, stageId, 1, Content: null, null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.ContentRequired);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_AddTriviaClue_WithValidContent_ReturnsClueId()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(
            new AddClueCommand(mission.Id, stageId, 1, "Use the Force", null, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _repositoryMock.Verify(r => r.AddClueAsync(It.IsAny<Clue>(), default), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_AddTreasureHuntClue_WithValidData_Succeeds()
    {
        var mission = MissionWithTreasureHuntStage(out var stageId);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(
            new AddClueCommand(mission.Id, stageId, 1, null, 10.48, -66.87, 50.0), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBe(Guid.Empty);
        _repositoryMock.Verify(r => r.AddClueAsync(It.IsAny<Clue>(), default), Times.Once);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WithDuplicateOrder_ReturnsDuplicateClueOrder()
    {
        var mission = MissionWithTriviaStage(out var stageId);
        mission.AddClue(stageId, false, 1, "Existing hint", null, null, null);

        _repositoryMock.Setup(r => r.GetByIdAsync(mission.Id, default)).ReturnsAsync(mission);
        _repositoryMock.Setup(r => r.HasActiveSessionsAsync(mission.Id, default)).ReturnsAsync(false);

        var result = await _handler.Handle(
            new AddClueCommand(mission.Id, stageId, 1, "New hint", null, null, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.DuplicateClueOrder);
        _repositoryMock.Verify(r => r.SaveChangesAsync(default), Times.Never);
    }
}
