namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

public class TeamPenalizeTests
{
    [Fact]
    public void Penalize_ValidPointsAndReason_SubtractsFromScore()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Alpha").Value);
        team.UpdateScore(100);

        var result = team.Penalize(30, "Unsporting behavior");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(70);
        team.Score.Should().Be(70);
    }

    [Fact]
    public void Penalize_MoreThanScore_ScoreGoesNegative()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Beta").Value);
        team.UpdateScore(10);

        var result = team.Penalize(50, "Major infraction");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(-40);
        team.Score.Should().Be(-40);
    }

    [Fact]
    public void Penalize_ZeroPoints_ReturnsError()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Gamma").Value);
        var result = team.Penalize(0, "Some reason");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.InvalidPenaltyPoints);
        team.Score.Should().Be(0); // unchanged
    }

    [Fact]
    public void Penalize_NegativePoints_ReturnsError()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Delta").Value);
        var result = team.Penalize(-5, "Some reason");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.InvalidPenaltyPoints);
    }

    [Fact]
    public void Penalize_EmptyReason_ReturnsError()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Epsilon").Value);
        team.UpdateScore(50);

        var result = team.Penalize(10, "  ");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.PenaltyReasonRequired);
        team.Score.Should().Be(50); // unchanged
    }

    [Fact]
    public void Penalize_NullReason_ReturnsError()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Zeta").Value);
        team.UpdateScore(50);

        var result = team.Penalize(10, null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.PenaltyReasonRequired);
    }
}
