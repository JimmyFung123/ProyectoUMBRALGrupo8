namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

public class TeamJoinTests
{
    [Fact]
    public void Create_GeneratesNonEmptyInviteCode()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        team.InviteCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Create_InviteCodeHasFourChars()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        team.InviteCode.Should().HaveLength(4);
    }

    [Fact]
    public void Create_MemberCountStartsAtOne()
    {
        var team = Team.Create(Guid.NewGuid(), "Beta");
        team.MemberCount.Should().Be(1);
    }

    [Fact]
    public void Join_IncrementsMemberCount()
    {
        var team = Team.Create(Guid.NewGuid(), "Gamma");
        team.Join();
        team.MemberCount.Should().Be(2);
    }

    [Fact]
    public void Join_MultipleTimes_AccumulatesCount()
    {
        var team = Team.Create(Guid.NewGuid(), "Delta");
        team.Join();
        team.Join();
        team.MemberCount.Should().Be(3);
    }

    [Fact]
    public void Create_TwoTeams_HaveDifferentInviteCodes()
    {
        var t1 = Team.Create(Guid.NewGuid(), "Team 1");
        var t2 = Team.Create(Guid.NewGuid(), "Team 2");
        t1.InviteCode.Should().NotBe(t2.InviteCode);
    }
}
