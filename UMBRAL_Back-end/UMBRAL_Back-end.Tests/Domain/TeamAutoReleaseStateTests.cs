namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

public class TeamAutoReleaseStateTests
{
    [Fact]
    public void ReceiveClue_Manual_SetsLastClueWasAutomaticFalse()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Alpha").Value);
        team.ReceiveClue(3);
        team.LastClueWasAutomatic.Should().BeFalse();
        team.ClueTimerResetAt.Should().NotBeNull();
    }

    [Fact]
    public void ReceiveClue_Automatic_SetsLastClueWasAutomaticTrue()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Beta").Value);
        team.ReceiveClue(3, isAutomatic: true);
        team.LastClueWasAutomatic.Should().BeTrue();
        team.ClueTimerResetAt.Should().NotBeNull();
    }

    [Fact]
    public void UpdateProgress_NewStage_ResetsTimer()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Gamma").Value);
        team.UpdateProgress(1, 0, 0);
        team.ClueTimerResetAt.Should().NotBeNull();
        team.LastClueWasAutomatic.Should().BeFalse();
    }

    [Fact]
    public void UpdateProgress_SameStage_DoesNotResetTimer()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Delta").Value);
        team.UpdateProgress(1, 0, 0);
        var firstReset = team.ClueTimerResetAt;
        // same stage, different clue count — timer should not be reset
        team.UpdateProgress(1, 1, 2);
        team.ClueTimerResetAt.Should().Be(firstReset);
    }

    [Fact]
    public void NewTeam_ClueTimerResetAt_IsNull()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Epsilon").Value);
        team.ClueTimerResetAt.Should().BeNull();
        team.LastClueWasAutomatic.Should().BeFalse();
    }
}
