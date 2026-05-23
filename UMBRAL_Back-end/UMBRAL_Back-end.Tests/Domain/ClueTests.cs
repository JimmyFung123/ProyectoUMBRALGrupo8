namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class ClueTests
{
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

    private static Mission ActiveMission(out Guid stageId)
    {
        var mission = MissionWithTriviaStage(out stageId);
        mission.Activate();
        return mission;
    }

    // ── AddClue: mission locked ───────────────────────────────────────────────────

    [Fact]
    public void AddClue_WhenMissionIsActive_ReturnsStageLocked()
    {
        var mission = ActiveMission(out var stageId);

        var result = mission.AddClue(stageId, hasActiveSessions: false, order: 1, content: "Hint", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.StageLocked);
    }

    [Fact]
    public void AddClue_WhenHasActiveSessions_ReturnsStageLocked()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        var result = mission.AddClue(stageId, hasActiveSessions: true, order: 1, content: "Hint", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.StageLocked);
    }

    // ── AddClue: stage not found ──────────────────────────────────────────────────

    [Fact]
    public void AddClue_WhenStageNotFound_ReturnsStageNotFound()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddClue(Guid.NewGuid(), false, 1, "Hint", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
    }

    // ── AddClue: Trivia ───────────────────────────────────────────────────────────

    [Fact]
    public void AddClue_Trivia_WithoutContent_ReturnsContentRequired()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        var result = mission.AddClue(stageId, false, 1, content: null, null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.ContentRequired);
    }

    [Fact]
    public void AddClue_Trivia_WithEmptyContent_ReturnsContentRequired()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        var result = mission.AddClue(stageId, false, 1, content: "  ", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.ContentRequired);
    }

    [Fact]
    public void AddClue_Trivia_WithValidContent_Succeeds()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        var result = mission.AddClue(stageId, false, 1, "This is the hint", null, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().NotBe(Guid.Empty);
        result.Value.Content.Should().Be("This is the hint");
        result.Value.Order.Should().Be(1);
        mission.Stages.First().Clues.Should().HaveCount(1);
    }

    // ── AddClue: TreasureHunt ─────────────────────────────────────────────────────

    [Fact]
    public void AddClue_TreasureHunt_WithoutCoordinates_ReturnsCoordinatesRequired()
    {
        var mission = MissionWithTreasureHuntStage(out var stageId);

        var result = mission.AddClue(stageId, false, 1, null, latitude: null, longitude: null, radiusMeters: 100);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.CoordinatesRequired);
    }

    [Fact]
    public void AddClue_TreasureHunt_WithoutRadius_ReturnsRadiusRequired()
    {
        var mission = MissionWithTreasureHuntStage(out var stageId);

        var result = mission.AddClue(stageId, false, 1, null, 10.48, -66.87, radiusMeters: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.RadiusRequired);
    }

    [Fact]
    public void AddClue_TreasureHunt_WithAllFields_Succeeds()
    {
        var mission = MissionWithTreasureHuntStage(out var stageId);

        var result = mission.AddClue(stageId, false, 1, null, 10.48, -66.87, 50.0);

        result.IsSuccess.Should().BeTrue();
        result.Value.Latitude.Should().Be(10.48);
        result.Value.Longitude.Should().Be(-66.87);
        result.Value.RadiusMeters.Should().Be(50.0);
        result.Value.Content.Should().BeNull();
    }

    // ── AddClue: order validation ─────────────────────────────────────────────────

    [Fact]
    public void AddClue_WithZeroOrder_ReturnsInvalidOrder()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        var result = mission.AddClue(stageId, false, order: 0, "Hint", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.InvalidOrder);
    }

    [Fact]
    public void AddClue_WithDuplicateOrder_ReturnsDuplicateClueOrder()
    {
        var mission = MissionWithTriviaStage(out var stageId);
        mission.AddClue(stageId, false, 1, "First hint", null, null, null);

        var result = mission.AddClue(stageId, false, 1, "Second hint", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.DuplicateClueOrder);
    }

    // ── UpdateClue ────────────────────────────────────────────────────────────────

    [Fact]
    public void UpdateClue_WhenMissionIsActive_ReturnsStageLocked()
    {
        var mission = ActiveMission(out var stageId);

        var result = mission.UpdateClue(stageId, Guid.NewGuid(), false, 1, "Hint", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.StageLocked);
    }

    [Fact]
    public void UpdateClue_WhenClueNotFound_ReturnsClueNotFound()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        var result = mission.UpdateClue(stageId, Guid.NewGuid(), false, 1, "Hint", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.NotFound);
    }

    [Fact]
    public void UpdateClue_WithOrderTakenByAnotherClue_ReturnsDuplicateClueOrder()
    {
        var mission = MissionWithTriviaStage(out var stageId);
        mission.AddClue(stageId, false, 1, "First hint", null, null, null);
        mission.AddClue(stageId, false, 2, "Second hint", null, null, null);
        var clueId = mission.Stages.First().Clues.Last().Id;

        // Try to move clue from order 2 to order 1 (already taken)
        var result = mission.UpdateClue(stageId, clueId, false, 1, "Updated hint", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.DuplicateClueOrder);
    }

    [Fact]
    public void UpdateClue_WithSameOrderAsItself_Succeeds()
    {
        var mission = MissionWithTriviaStage(out var stageId);
        mission.AddClue(stageId, false, 1, "First hint", null, null, null);
        mission.AddClue(stageId, false, 2, "Second hint", null, null, null);
        var clueId = mission.Stages.First().Clues.Last().Id;

        var result = mission.UpdateClue(stageId, clueId, false, 2, "Updated second hint", null, null, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Updated second hint");
    }

    // ── RemoveClue ────────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveClue_WhenMissionIsActive_ReturnsStageLocked()
    {
        var mission = ActiveMission(out var stageId);

        var result = mission.RemoveClue(stageId, Guid.NewGuid(), hasActiveSessions: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.StageLocked);
    }

    [Fact]
    public void RemoveClue_WhenClueNotFound_ReturnsClueNotFound()
    {
        var mission = MissionWithTriviaStage(out var stageId);

        var result = mission.RemoveClue(stageId, Guid.NewGuid(), hasActiveSessions: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.NotFound);
    }

    [Fact]
    public void RemoveClue_WithValidClue_Succeeds()
    {
        var mission = MissionWithTriviaStage(out var stageId);
        mission.AddClue(stageId, false, 1, "Hint to remove", null, null, null);
        var clueId = mission.Stages.First().Clues.First().Id;

        var result = mission.RemoveClue(stageId, clueId, hasActiveSessions: false);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.First().Clues.Should().BeEmpty();
    }
}
