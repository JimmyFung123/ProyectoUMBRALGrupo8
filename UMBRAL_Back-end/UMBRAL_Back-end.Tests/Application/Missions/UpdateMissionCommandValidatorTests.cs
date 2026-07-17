namespace UMBRAL_Back_end.Tests.Application.Missions;

using FluentAssertions;
using UMBRAL_Back_end.Application.Missions.Commands.UpdateMission;
using Xunit;

public class UpdateMissionCommandValidatorTests
{
    private readonly UpdateMissionCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        var result = _validator.Validate(
            new UpdateMissionCommand(Guid.NewGuid(), "Alpha Protocol", "desc", "Medium", 60));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_FailsOnNameField()
    {
        var result = _validator.Validate(
            new UpdateMissionCommand(Guid.NewGuid(), "", "desc", "Medium", 60));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateMissionCommand.Name));
    }

    [Fact]
    public void Validate_UnknownDifficulty_FailsOnDifficultyField()
    {
        var result = _validator.Validate(
            new UpdateMissionCommand(Guid.NewGuid(), "Alpha", "desc", "Imposible", 60));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateMissionCommand.Difficulty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_NonPositiveMaxDuration_FailsOnMaxDurationField(int maxDuration)
    {
        var result = _validator.Validate(
            new UpdateMissionCommand(Guid.NewGuid(), "Alpha", "desc", "Medium", maxDuration));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UpdateMissionCommand.MaxDuration));
    }
}
