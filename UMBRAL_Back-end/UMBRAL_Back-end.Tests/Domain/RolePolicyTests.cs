namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UserService.Domain.Users;
using Xunit;

/// <summary>
/// HU-23 Criterio 4: RolePolicy domain service — reglas de protección al
/// último administrador y de no auto-acción, ahora centralizadas y puras.
/// </summary>
public class RolePolicyTests
{
    // ── IsDemotion ──────────────────────────────────────────────────────────

    [Fact]
    public void IsDemotion_AdminToOperator_IsTrue()
        => RolePolicy.IsDemotion(UserRole.Admin, UserRole.Operator).Should().BeTrue();

    [Theory]
    [InlineData(UserRole.Operator, UserRole.Admin)]   // promoción
    [InlineData(UserRole.Admin, UserRole.Admin)]      // sin cambio
    [InlineData(UserRole.Operator, UserRole.Operator)]
    public void IsDemotion_OtherTransitions_IsFalse(UserRole current, UserRole next)
        => RolePolicy.IsDemotion(current, next).Should().BeFalse();

    [Fact]
    public void IsDemotion_WhenCurrentRoleNull_IsFalse()
        => RolePolicy.IsDemotion(null, UserRole.Operator).Should().BeFalse();

    // ── CountActiveAdmins ───────────────────────────────────────────────────

    [Fact]
    public void CountActiveAdmins_CountsOnlyEnabledAdmins()
    {
        var users = new (UserRole?, bool)[]
        {
            (UserRole.Admin, true),
            (UserRole.Admin, false),     // deshabilitado → no cuenta
            (UserRole.Operator, true),   // operador → no cuenta
            (null, true),                // sin rol → no cuenta
            (UserRole.Admin, true),
        };

        RolePolicy.CountActiveAdmins(users).Should().Be(2);
    }

    // ── IsLastActiveAdmin ───────────────────────────────────────────────────

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public void IsLastActiveAdmin_TrueWhenOneOrFewer(int count, bool expected)
        => RolePolicy.IsLastActiveAdmin(count).Should().Be(expected);

    // ── IsSelf ──────────────────────────────────────────────────────────────

    [Fact]
    public void IsSelf_WhenEmailsMatchCaseInsensitive_IsTrue()
        => RolePolicy.IsSelf("admin@umbral.local", "ADMIN@UMBRAL.LOCAL").Should().BeTrue();

    [Fact]
    public void IsSelf_WhenRequesterHasWhitespace_StillMatches()
        => RolePolicy.IsSelf("admin@umbral.local", "  admin@umbral.local ").Should().BeTrue();

    [Theory]
    [InlineData("admin@umbral.local", "otro@umbral.local")]
    [InlineData("admin@umbral.local", null)]
    [InlineData("admin@umbral.local", "")]
    [InlineData("admin@umbral.local", "   ")]
    public void IsSelf_WhenDifferentOrMissing_IsFalse(string target, string? requester)
        => RolePolicy.IsSelf(target, requester).Should().BeFalse();
}
