namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UserService.Domain.Users;
using Xunit;

/// <summary>
/// HU-23: PersonName VO — nombre y apellido obligatorios + trim.
/// </summary>
public class PersonNameTests
{
    [Theory]
    [InlineData("", "Apellido")]
    [InlineData("   ", "Apellido")]
    [InlineData("Nombre", "")]
    [InlineData("Nombre", "   ")]
    [InlineData(null, "Apellido")]
    [InlineData("Nombre", null)]
    public void Create_WhenEitherPartBlank_ReturnsInvalidName(string? first, string? last)
    {
        var result = PersonName.Create(first, last);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UserErrors.InvalidName);
    }

    [Fact]
    public void Create_WhenValid_TrimsBothParts()
    {
        var result = PersonName.Create("  Nombre  ", "  Apellido ");

        result.IsSuccess.Should().BeTrue();
        result.Value.FirstName.Should().Be("Nombre");
        result.Value.LastName.Should().Be("Apellido");
        result.Value.ToString().Should().Be("Nombre Apellido");
    }
}
