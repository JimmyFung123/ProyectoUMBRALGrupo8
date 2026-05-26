namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UserService.Domain.Users;
using Xunit;

public class UserRoleTests
{
    [Theory]
    [InlineData(UserRole.Admin, "admin")]
    [InlineData(UserRole.Operator, "operator")]
    public void ToKeycloakName_ReturnsLowercaseRealmRoleName(UserRole role, string expected)
    {
        role.ToKeycloakName().Should().Be(expected);
    }

    [Theory]
    [InlineData("admin", UserRole.Admin)]
    [InlineData("operator", UserRole.Operator)]
    public void FromKeycloakName_ParsesKnownRoles(string name, UserRole expected)
    {
        UserRoleExtensions.FromKeycloakName(name).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("uma_authorization")]   // default realm role
    [InlineData("offline_access")]
    [InlineData("Admin")]               // case-sensitive: solo lowercase es válido
    public void FromKeycloakName_ReturnsNullForUnknownRoles(string name)
    {
        UserRoleExtensions.FromKeycloakName(name).Should().BeNull();
    }
}
