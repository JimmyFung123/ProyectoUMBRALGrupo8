namespace UMBRAL_Back_end.Tests.Application.Missions;

using FluentAssertions;
using UMBRAL_Back_end.Application.Missions.Commands.CreateMission;
using Xunit;

public class CreateMissionCommandValidatorTests
{
    private readonly CreateMissionCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        var result = _validator.Validate(new CreateMissionCommand("Alpha Protocol", "desc", "Medium", 60));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyName_FailsOnNameField()
    {
        var result = _validator.Validate(new CreateMissionCommand("", "desc", "Medium", 60));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateMissionCommand.Name));
    }

    [Fact]
    public void Validate_UnknownDifficulty_FailsOnDifficultyField()
    {
        var result = _validator.Validate(new CreateMissionCommand("Alpha", "desc", "Imposible", 60));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateMissionCommand.Difficulty));
    }

    [Theory]
    [InlineData("easy")]
    [InlineData("MEDIUM")]
    [InlineData("Hard")]
    public void Validate_DifficultyCaseInsensitive_Succeeds(string difficulty)
    {
        var result = _validator.Validate(new CreateMissionCommand("Alpha", "desc", difficulty, 60));

        result.Errors.Should().NotContain(e => e.PropertyName == nameof(CreateMissionCommand.Difficulty));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_NonPositiveMaxDuration_FailsOnMaxDurationField(int maxDuration)
    {
        var result = _validator.Validate(new CreateMissionCommand("Alpha", "desc", "Medium", maxDuration));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateMissionCommand.MaxDuration));
    }
}
