namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

public class TeamForceAdvanceTests
{
    [Fact]
    public void ForceAdvance_ToNextStage_SetsStageOrderAndResetsClues()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        team.UpdateProgress(1, 2, 5); // on stage 1, 2 clues received

        var result = team.ForceAdvance(2);

        result.IsSuccess.Should().BeTrue();
        team.CurrentStageOrder.Should().Be(2);
        team.CluesReceivedCurrentStage.Should().Be(0);
        team.LastClueWasAutomatic.Should().BeFalse();
        team.ClueTimerResetAt.Should().NotBeNull();
    }

    [Fact]
    public void ForceAdvance_DoesNotChangeScore()
    {
        var team = Team.Create(Guid.NewGuid(), "Beta");
        team.UpdateScore(150);
        team.UpdateProgress(1, 0, 3);

        team.ForceAdvance(2);

        team.Score.Should().Be(150); // unchanged
    }

    [Fact]
    public void ForceAdvance_FromStageZero_ToStageOne_Succeeds()
    {
        var team = Team.Create(Guid.NewGuid(), "Gamma");
        // CurrentStageOrder starts at 0

        var result = team.ForceAdvance(1);

        result.IsSuccess.Should().BeTrue();
        team.CurrentStageOrder.Should().Be(1);
    }

    [Fact]
    public void ForceAdvance_SameStageOrder_ReturnsError()
    {
        var team = Team.Create(Guid.NewGuid(), "Delta");
        team.UpdateProgress(2, 0, 3);

        var result = team.ForceAdvance(2); // same order

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.InvalidNextStage);
        team.CurrentStageOrder.Should().Be(2); // unchanged
    }

    [Fact]
    public void ForceAdvance_LowerStageOrder_ReturnsError()
    {
        var team = Team.Create(Guid.NewGuid(), "Epsilon");
        team.UpdateProgress(3, 0, 3);

        var result = team.ForceAdvance(1); // going backwards

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.InvalidNextStage);
    }
}
