namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UserService.Domain.Users;
using Xunit;

/// <summary>
/// HU-23: EmailAddress VO — formato válido + normalización (trim/lowercase).
/// </summary>
public class EmailAddressTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin-arroba")]
    [InlineData("@sin-local")]
    [InlineData("sin-dominio@")]
    [InlineData(null)]
    public void Create_WhenInvalid_ReturnsInvalidEmail(string? raw)
    {
        var result = EmailAddress.Create(raw);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidEmail);
    }

    [Fact]
    public void Create_WhenValid_Succeeds()
    {
        var result = EmailAddress.Create("nuevo@umbral.local");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("nuevo@umbral.local");
    }

    [Fact]
    public void Create_TrimsAndLowercases()
    {
        var result = EmailAddress.Create("  USUARIO@Umbral.LOCAL  ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be("usuario@umbral.local");
    }

    [Fact]
    public void Equality_IsByNormalizedValue()
    {
        var a = EmailAddress.Create("Admin@Umbral.Local").Value;
        var b = EmailAddress.Create("  admin@umbral.local ").Value;

        a.Should().Be(b);
    }
}
