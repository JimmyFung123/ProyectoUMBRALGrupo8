namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class MissionTests
{
    // ── Creation ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidData_ReturnsMission()
    {
        var result = Mission.Create("Alpha Protocol", "First mission", DifficultyLevel.Medium, 60);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Alpha Protocol");
        result.Value.Difficulty.Should().Be(DifficultyLevel.Medium);
        result.Value.MaxDuration.Should().Be(60);
        result.Value.Status.Should().Be(MissionStatus.Inactive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null!)]
    public void Create_WithEmptyName_ReturnsInvalidNameError(string? name)
    {
        var result = Mission.Create(name!, "desc", DifficultyLevel.Easy, 30);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.InvalidName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithInvalidMaxDuration_ReturnsInvalidMaxDurationError(int duration)
    {
        var result = Mission.Create("Test", "desc", DifficultyLevel.Easy, duration);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.InvalidMaxDuration);
    }

    [Fact]
    public void Create_TrimsNameWhitespace()
    {
        var result = Mission.Create("  Trimmed  ", "desc", DifficultyLevel.Hard, 45);

        result.Value.Name.Should().Be("Trimmed");
    }

    // ── Activation ──────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_SetsStatusToActive()
    {
        // Stage validation is owned by StageService — MissionService activates without checking stages.
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.Activate();

        result.IsSuccess.Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Active);
        mission.UpdatedAt.Should().NotBeNull();
    }

    // ── Deactivation ────────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_WhenHasActiveSessions_ReturnsHasActiveSessionsError()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Medium, 60).Value;

        var result = mission.Deactivate(hasActiveSessions: true);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.HasActiveSessions);
    }

    [Fact]
    public void Deactivate_WhenNoActiveSessions_Succeeds()
    {
        var mission = Mission.Create("Test", "desc", DifficultyLevel.Medium, 60).Value;

        var result = mission.Deactivate(hasActiveSessions: false);

        result.IsSuccess.Should().BeTrue();
        mission.Status.Should().Be(MissionStatus.Inactive);
        mission.UpdatedAt.Should().NotBeNull();
    }

    // ── Update ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WhenHasActiveSessions_ReturnsHasActiveSessionsError()
    {
        var mission = Mission.Create("Old Name", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.Update("New Name", "new desc", DifficultyLevel.Hard, 90, hasActiveSessions: true);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.HasActiveSessions);
        mission.Name.Should().Be("Old Name");
    }

    [Fact]
    public void Update_WithEmptyName_ReturnsInvalidNameError()
    {
        var mission = Mission.Create("Old Name", "desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.Update("", "desc", DifficultyLevel.Medium, 60, hasActiveSessions: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.InvalidName);
    }

    [Fact]
    public void Update_WithValidData_ChangesAllProperties()
    {
        var mission = Mission.Create("Old Name", "old desc", DifficultyLevel.Easy, 30).Value;

        var result = mission.Update("New Name", "new desc", DifficultyLevel.Hard, 120, hasActiveSessions: false);

        result.IsSuccess.Should().BeTrue();
        mission.Name.Should().Be("New Name");
        mission.Description.Should().Be("new desc");
        mission.Difficulty.Should().Be(DifficultyLevel.Hard);
        mission.MaxDuration.Should().Be(120);
        mission.UpdatedAt.Should().NotBeNull();
    }
}
