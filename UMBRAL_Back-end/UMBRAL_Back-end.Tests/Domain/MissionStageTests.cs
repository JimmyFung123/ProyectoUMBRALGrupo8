namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class MissionStageTests
{
    // ── AddStage: RB-14 (Active mission locked) ─────────────────────────────────

    [Fact]
    public void AddStage_WhenMissionIsActive_ReturnsMissionLockedError()
    {
        // A mission with one stage can be activated
        var mission = CreateActivatableMission();

        // Try to add another stage after activation
        var result = mission.AddStage("Stage 2", 2, StageType.Trivia, 100, hasActiveSessions: false, question: "Q?");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
    }

    [Fact]
    public void AddStage_WhenHasActiveSessions_ReturnsMissionLockedError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage("Stage 1", 1, StageType.Trivia, 50, hasActiveSessions: true, question: "Q?");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
    }

    // ── AddStage: Trivia ────────────────────────────────────────────────────────

    [Fact]
    public void AddStage_ValidTrivia_AddsStageSuccessfully()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage(
            title: "Trivia Stage",
            order: 1,
            type: StageType.Trivia,
            baseScore: 100,
            hasActiveSessions: false,
            question: "What is 2+2?",
            options: [("3", false), ("4", true), ("5", false)]);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.Should().HaveCount(1);
        result.Value.Type.Should().Be(StageType.Trivia);
        result.Value.Options.Should().HaveCount(3);
        result.Value.Options.Count(o => o.IsCorrect).Should().Be(1);
    }

    // ── AddStage: TreasureHunt (RB-20) ─────────────────────────────────────────

    [Fact]
    public void AddStage_TreasureHunt_WithoutCoordinates_ReturnsCoordinatesRequiredError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage(
            title: "Treasure Stage",
            order: 1,
            type: StageType.TreasureHunt,
            baseScore: 200,
            hasActiveSessions: false,
            qrCode: "QR-001");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.CoordinatesRequired);
    }

    [Fact]
    public void AddStage_TreasureHunt_WithoutQrCode_ReturnsQrCodeRequiredError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage(
            title: "Treasure Stage",
            order: 1,
            type: StageType.TreasureHunt,
            baseScore: 200,
            hasActiveSessions: false,
            latitude: 10.5,
            longitude: -66.9);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.QrCodeRequired);
    }

    [Fact]
    public void AddStage_TreasureHunt_WithAllRequiredFields_Succeeds()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage(
            title: "Treasure Stage",
            order: 1,
            type: StageType.TreasureHunt,
            baseScore: 200,
            hasActiveSessions: false,
            latitude: 10.48801,
            longitude: -66.87919,
            qrCode: "QR-UNIQUE-001");

        result.IsSuccess.Should().BeTrue();
        result.Value.Latitude.Should().Be(10.48801);
        result.Value.QrCode.Should().Be("QR-UNIQUE-001");
    }

    // ── RemoveStage: RB-14 ──────────────────────────────────────────────────────

    [Fact]
    public void RemoveStage_WhenMissionIsActive_ReturnsMissionLockedError()
    {
        var mission = CreateActivatableMission();
        var stageId = mission.Stages.First().Id;

        var result = mission.RemoveStage(stageId, hasActiveSessions: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
    }

    [Fact]
    public void RemoveStage_NonExistentStage_ReturnsNotFoundError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.RemoveStage(Guid.NewGuid(), hasActiveSessions: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
    }

    // ── Activation after stages configured (RB-13) ─────────────────────────────

    [Fact]
    public void Activate_AfterAddingStage_Succeeds()
    {
        var mission = CreateActivatableMission();

        mission.Status.Should().Be(MissionStatus.Active);
        mission.Stages.Should().HaveCount(1);
    }

    // ── UpdateStage: RB-14 ──────────────────────────────────────────────────────

    [Fact]
    public void UpdateStage_WhenMissionIsActive_ReturnsMissionLockedError()
    {
        var mission = CreateActivatableMission();
        var stageId = mission.Stages.First().Id;

        var result = mission.UpdateStage(stageId, false, "New Title", 1, 200, "New Q?", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
    }

    [Fact]
    public void UpdateStage_WhenHasActiveSessions_ReturnsMissionLockedError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage 1", 1, StageType.Trivia, 100, false, question: "Q?");
        var stageId = mission.Stages.First().Id;

        var result = mission.UpdateStage(stageId, true, "New Title", 1, 200, "New Q?", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
    }

    [Fact]
    public void UpdateStage_WhenStageNotFound_ReturnsNotFoundError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.UpdateStage(Guid.NewGuid(), false, "Title", 1, 100, null, null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
    }

    [Fact]
    public void UpdateStage_Trivia_WithValidData_ChangesFields()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Old Title", 1, StageType.Trivia, 50, false, question: "Old Q?");
        var stageId = mission.Stages.First().Id;

        var result = mission.UpdateStage(stageId, false, "New Title", 2, 150, "New Q?", null, null, null);

        result.IsSuccess.Should().BeTrue();
        var stage = mission.Stages.First();
        stage.Title.Should().Be("New Title");
        stage.Order.Should().Be(2);
        stage.BaseScore.Should().Be(150);
        stage.Question.Should().Be("New Q?");
        mission.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateStage_TreasureHunt_WithoutCoordinates_ReturnsCoordinatesRequiredError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.TreasureHunt, 100, false,
            latitude: 10.5, longitude: -66.9, qrCode: "QR-001");
        var stageId = mission.Stages.First().Id;

        // Try to update without coordinates
        var result = mission.UpdateStage(stageId, false, "Stage", 1, 100, null, null, null, "QR-001");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.CoordinatesRequired);
    }

    [Fact]
    public void UpdateStage_TreasureHunt_WithValidData_ChangesCoordinates()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.TreasureHunt, 100, false,
            latitude: 10.5, longitude: -66.9, qrCode: "QR-001");
        var stageId = mission.Stages.First().Id;

        var result = mission.UpdateStage(stageId, false, "Stage Updated", 1, 200,
            null, 10.99, -67.01, "QR-001");

        result.IsSuccess.Should().BeTrue();
        var stage = mission.Stages.First();
        stage.Title.Should().Be("Stage Updated");
        stage.Latitude.Should().Be(10.99);
        stage.Longitude.Should().Be(-67.01);
        stage.BaseScore.Should().Be(200);
    }

    // ── Gap 1: Duplicate order ───────────────────────────────────────────────────

    [Fact]
    public void AddStage_WithDuplicateOrder_ReturnsDuplicateStageOrderError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage 1", 1, StageType.Trivia, 100, false, question: "Q?");

        var result = mission.AddStage("Stage 2", 1, StageType.Trivia, 100, false, question: "Q2?");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.DuplicateStageOrder);
    }

    [Fact]
    public void UpdateStage_WithOrderTakenByAnotherStage_ReturnsDuplicateStageOrderError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage 1", 1, StageType.Trivia, 100, false, question: "Q1?");
        mission.AddStage("Stage 2", 2, StageType.Trivia, 100, false, question: "Q2?");
        var stageId = mission.Stages.Last().Id;

        // Try to update Stage 2's order to 1 (already taken by Stage 1)
        var result = mission.UpdateStage(stageId, false, "Stage 2", 1, 100, "Q2?", null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.DuplicateStageOrder);
    }

    [Fact]
    public void UpdateStage_WithSameOrderAsItself_Succeeds()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage 1", 1, StageType.Trivia, 100, false, question: "Q1?");
        mission.AddStage("Stage 2", 2, StageType.Trivia, 100, false, question: "Q2?");
        var stageId = mission.Stages.Last().Id;

        // Updating Stage 2 keeping order = 2 should succeed (not a duplicate with itself)
        var result = mission.UpdateStage(stageId, false, "Stage 2 Updated", 2, 200, "Q2 updated?", null, null, null);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Gap 2: Trivia options validation ─────────────────────────────────────────

    [Fact]
    public void AddStage_Trivia_WithNoOptions_ReturnsTriviaRequiresAtLeastTwoOptionsError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage("Trivia", 1, StageType.Trivia, 100, false,
            question: "Q?", options: []);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.TriviaRequiresAtLeastTwoOptions);
    }

    [Fact]
    public void AddStage_Trivia_WithOneOption_ReturnsTriviaRequiresAtLeastTwoOptionsError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage("Trivia", 1, StageType.Trivia, 100, false,
            question: "Q?", options: [("Only option", true)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.TriviaRequiresAtLeastTwoOptions);
    }

    [Fact]
    public void AddStage_Trivia_WithTwoOptionsButNoneCorrect_ReturnsTriviaRequiresExactlyOneCorrectOptionError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage("Trivia", 1, StageType.Trivia, 100, false,
            question: "Q?", options: [("A", false), ("B", false)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.TriviaRequiresExactlyOneCorrectOption);
    }

    [Fact]
    public void AddStage_Trivia_WithTwoOptionsAndTwoCorrect_ReturnsTriviaRequiresExactlyOneCorrectOptionError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage("Trivia", 1, StageType.Trivia, 100, false,
            question: "Q?", options: [("A", true), ("B", true)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.TriviaRequiresExactlyOneCorrectOption);
    }

    [Fact]
    public void AddStage_Trivia_WithTwoOptionsAndExactlyOneCorrect_Succeeds()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.AddStage("Trivia", 1, StageType.Trivia, 100, false,
            question: "Q?", options: [("A", false), ("B", true)]);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.Should().HaveCount(1);
        result.Value.Options.Count(o => o.IsCorrect).Should().Be(1);
    }

    [Fact]
    public void UpdateStage_Trivia_WithZeroCorrectOptions_ReturnsTriviaRequiresExactlyOneCorrectOptionError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.Trivia, 100, false, question: "Q?",
            options: [("A", false), ("B", true)]);
        var stageId = mission.Stages.First().Id;

        var result = mission.UpdateStage(stageId, false, "Stage", 1, 100, "Q?",
            null, null, null,
            options: [("X", false), ("Y", false)]);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.TriviaRequiresExactlyOneCorrectOption);
    }

    [Fact]
    public void UpdateStage_Trivia_WithTwoOptionsAndOneCorrect_Succeeds()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.Trivia, 100, false, question: "Q?",
            options: [("A", false), ("B", true)]);
        var stageId = mission.Stages.First().Id;

        var result = mission.UpdateStage(stageId, false, "Stage Updated", 1, 200, "Q updated?",
            null, null, null,
            options: [("X", false), ("Y", true)]);

        result.IsSuccess.Should().BeTrue();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static Mission CreateActivatableMission()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage 1", 1, StageType.Trivia, 100, false, question: "Q?");
        mission.Activate();
        return mission;
    }
}
