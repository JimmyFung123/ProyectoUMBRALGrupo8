namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using SessionService.Application.Sessions.Commands.PenalizeTeam;
using Xunit;

public class PenalizeTeamCommandValidatorTests
{
    private readonly PenalizeTeamCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_HasNoErrors()
    {
        var result = _validator.Validate(
            new PenalizeTeamCommand(Guid.NewGuid(), Guid.NewGuid(), 10, "Conducta indebida"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyReason_FailsOnReasonField()
    {
        var result = _validator.Validate(
            new PenalizeTeamCommand(Guid.NewGuid(), Guid.NewGuid(), 10, ""));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(PenalizeTeamCommand.Reason));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Validate_NonPositivePoints_FailsOnPointsField(int points)
    {
        var result = _validator.Validate(
            new PenalizeTeamCommand(Guid.NewGuid(), Guid.NewGuid(), points, "Motivo válido"));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(PenalizeTeamCommand.Points));
    }
}
