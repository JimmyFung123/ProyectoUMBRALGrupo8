namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

/// <summary>
/// TeamMembershipPolicy domain service — reglas de membresía del equipo:
/// mínimo para competir (RB-18), capacidad máxima y vacío tras abandono.
/// </summary>
public class TeamMembershipPolicyTests
{
    // ── Umbrales ────────────────────────────────────────────────────────────

    [Fact]
    public void Thresholds_HaveExpectedValues()
    {
        TeamMembershipPolicy.MinimumMembers.Should().Be(2);   // RB-18
        TeamMembershipPolicy.MaximumMembers.Should().Be(6);
    }

    // ── CanJoin ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]   // hay cupo para el sexto
    [InlineData(6, false)]  // lleno
    [InlineData(7, false)]
    public void CanJoin_TrueWhileBelowMaximum(int currentMembers, bool expected)
        => TeamMembershipPolicy.CanJoin(currentMembers).Should().Be(expected);

    // ── MeetsStartRequirement (RB-18) ────────────────────────────────────────

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(6, true)]
    public void MeetsStartRequirement_TrueFromTwoMembers(int memberCount, bool expected)
        => TeamMembershipPolicy.MeetsStartRequirement(memberCount).Should().Be(expected);

    // ── IsEmptyAfterLeave ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(3, false)]
    public void IsEmptyAfterLeave_TrueOnlyAtZero(int memberCountAfterLeave, bool expected)
        => TeamMembershipPolicy.IsEmptyAfterLeave(memberCountAfterLeave).Should().Be(expected);
}
