namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

public class TeamJoinTests
{
    [Fact]
    public void Create_GeneratesNonEmptyInviteCode()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Alpha").Value);
        team.InviteCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_InviteCodeHasFourChars()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Alpha").Value);
        team.InviteCode.Should().HaveLength(4);
    }

    [Fact]
    public void Create_MemberCountStartsAtOne()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Beta").Value);
        team.MemberCount.Should().Be(1);
    }

    [Fact]
    public void Join_IncrementsMemberCount()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Gamma").Value);
        team.Join();
        team.MemberCount.Should().Be(2);
    }

    [Fact]
    public void Join_MultipleTimes_AccumulatesCount()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Delta").Value);
        team.Join();
        team.Join();
        team.MemberCount.Should().Be(3);
    }

    [Fact]
    public void Create_TwoTeams_HaveDifferentInviteCodes()
    {
        var t1 = Team.Create(Guid.NewGuid(), TeamName.Create("Team 1").Value);
        var t2 = Team.Create(Guid.NewGuid(), TeamName.Create("Team 2").Value);
        t1.InviteCode.Should().NotBe(t2.InviteCode);
    }
}
