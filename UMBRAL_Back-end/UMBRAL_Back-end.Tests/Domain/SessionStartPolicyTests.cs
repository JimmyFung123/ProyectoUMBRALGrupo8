namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// HU-12: SessionStartPolicy domain service — precondiciones para arrancar una
/// sesión (RB-02 mínimo de equipos, RB-18 mínimo de miembros por equipo),
/// ahora centralizadas y puras.
/// </summary>
public class SessionStartPolicyTests
{
    // ── Umbrales ────────────────────────────────────────────────────────────

    [Fact]
    public void Thresholds_MatchBusinessRules()
    {
        SessionStartPolicy.MinimumEnrolledTeams.Should().Be(1);   // RB-02
        SessionStartPolicy.MinimumMembersPerTeam.Should().Be(2);  // RB-18
    }

    // ── MeetsTeamRequirement (RB-02) ─────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void MeetsTeamRequirement_TrueWhenAtLeastOneTeam(int enrolledTeams, bool expected)
        => SessionStartPolicy.MeetsTeamRequirement(enrolledTeams).Should().Be(expected);

    // ── MeetsMinimumMembers (RB-18) ──────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(6, true)]
    public void MeetsMinimumMembers_TrueFromTwoMembers(int memberCount, bool expected)
        => SessionStartPolicy.MeetsMinimumMembers(memberCount).Should().Be(expected);

    // ── AllTeamsMeetMinimumMembers (RB-18) ───────────────────────────────────

    [Fact]
    public void AllTeamsMeetMinimumMembers_TrueWhenEveryTeamMeetsMinimum()
        => SessionStartPolicy.AllTeamsMeetMinimumMembers(new[] { 2, 3, 6 }).Should().BeTrue();

    [Fact]
    public void AllTeamsMeetMinimumMembers_FalseWhenAnyTeamBelowMinimum()
        => SessionStartPolicy.AllTeamsMeetMinimumMembers(new[] { 3, 1, 4 }).Should().BeFalse();

    [Fact]
    public void AllTeamsMeetMinimumMembers_EmptyCollection_IsVacuouslyTrue()
        => SessionStartPolicy.AllTeamsMeetMinimumMembers(Array.Empty<int>()).Should().BeTrue();
}
