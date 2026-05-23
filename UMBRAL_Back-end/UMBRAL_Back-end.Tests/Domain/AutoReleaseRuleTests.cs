namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class AutoReleaseRuleTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static (Mission mission, Guid stageId) CreateMissionWithStage()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.Trivia, 100, false, question: "Q?");
        var stageId = mission.Stages.First().Id;
        return (mission, stageId);
    }

    private static Mission CreateActiveMission(out Guid stageId)
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;
        mission.AddStage("Stage", 1, StageType.Trivia, 100, false,
            question: "Q?",
            options: [("Option A", true), ("Option B", false)]);
        stageId = mission.Stages.First().Id;
        mission.Activate();
        return mission;
    }

    // ── Success cases ─────────────────────────────────────────────────────────────

    [Fact]
    public void ConfigureAutoRelease_ValidTimeMinutes_Succeeds()
    {
        var (mission, stageId) = CreateMissionWithStage();

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: false, timeMinutes: 5, maxAttempts: null);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.First().AutoReleaseTimeMinutes.Should().Be(5);
        mission.Stages.First().AutoReleaseMaxAttempts.Should().BeNull();
    }

    [Fact]
    public void ConfigureAutoRelease_ValidMaxAttempts_Succeeds()
    {
        var (mission, stageId) = CreateMissionWithStage();

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: false, timeMinutes: null, maxAttempts: 3);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.First().AutoReleaseMaxAttempts.Should().Be(3);
        mission.Stages.First().AutoReleaseTimeMinutes.Should().BeNull();
    }

    [Fact]
    public void ConfigureAutoRelease_BothValues_Succeeds()
    {
        var (mission, stageId) = CreateMissionWithStage();

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: false, timeMinutes: 10, maxAttempts: 2);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.First().AutoReleaseTimeMinutes.Should().Be(10);
        mission.Stages.First().AutoReleaseMaxAttempts.Should().Be(2);
    }

    [Fact]
    public void ConfigureAutoRelease_ClearRule_Succeeds()
    {
        var (mission, stageId) = CreateMissionWithStage();
        mission.ConfigureAutoRelease(stageId, false, 5, 3);

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: false, timeMinutes: null, maxAttempts: null);

        result.IsSuccess.Should().BeTrue();
        mission.Stages.First().AutoReleaseTimeMinutes.Should().BeNull();
        mission.Stages.First().AutoReleaseMaxAttempts.Should().BeNull();
    }

    // ── Validation failures ───────────────────────────────────────────────────────

    [Fact]
    public void ConfigureAutoRelease_InvalidTimeZero_ReturnsError()
    {
        var (mission, stageId) = CreateMissionWithStage();

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: false, timeMinutes: 0, maxAttempts: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.InvalidAutoReleaseTime);
    }

    [Fact]
    public void ConfigureAutoRelease_InvalidTimeNegative_ReturnsError()
    {
        var (mission, stageId) = CreateMissionWithStage();

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: false, timeMinutes: -1, maxAttempts: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.InvalidAutoReleaseTime);
    }

    [Fact]
    public void ConfigureAutoRelease_InvalidAttemptsZero_ReturnsError()
    {
        var (mission, stageId) = CreateMissionWithStage();

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: false, timeMinutes: null, maxAttempts: 0);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.InvalidAutoReleaseAttempts);
    }

    // ── Lock guards ───────────────────────────────────────────────────────────────

    [Fact]
    public void ConfigureAutoRelease_MissionActive_ReturnsMissionLockedError()
    {
        var mission = CreateActiveMission(out var stageId);

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: false, timeMinutes: 5, maxAttempts: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
    }

    [Fact]
    public void ConfigureAutoRelease_HasActiveSessions_ReturnsMissionLockedError()
    {
        var (mission, stageId) = CreateMissionWithStage();

        var result = mission.ConfigureAutoRelease(stageId, hasActiveSessions: true, timeMinutes: 5, maxAttempts: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.MissionLockedForEditing);
    }

    [Fact]
    public void ConfigureAutoRelease_StageNotFound_ReturnsNotFoundError()
    {
        var (mission, _) = CreateMissionWithStage();
        var randomStageId = Guid.NewGuid();

        var result = mission.ConfigureAutoRelease(randomStageId, hasActiveSessions: false, timeMinutes: 5, maxAttempts: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
    }
}
