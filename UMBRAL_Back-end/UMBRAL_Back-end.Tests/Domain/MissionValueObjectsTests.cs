namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

/// <summary>
/// Mission Design value objects (v2): MissionName, MissionDescription, SessionDuration.
/// </summary>
public class MissionValueObjectsTests
{
    // ── MissionName ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MissionName_WhenBlank_ReturnsInvalidName(string? raw)
    {
        var result = MissionName.Create(raw);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.InvalidName);
    }

    [Fact]
    public void MissionName_TrimsWhitespace()
    {
        MissionName.Create("  Alpha  ").Value.Value.Should().Be("Alpha");
    }

    [Fact]
    public void MissionName_WhenTooLong_ReturnsInvalidName()
    {
        var tooLong = new string('a', MissionName.MaxLength + 1);
        MissionName.Create(tooLong).Error.Should().Be(MissionErrors.InvalidName);
    }

    [Fact]
    public void MissionName_EqualityIsByValue()
    {
        MissionName.Create("Alpha").Value.Should().Be(MissionName.Create("  Alpha ").Value);
    }

    // ── MissionDescription ───────────────────────────────────────────────────

    [Fact]
    public void MissionDescription_AllowsEmpty()
    {
        var result = MissionDescription.Create("   ");

        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(string.Empty);
    }

    [Fact]
    public void MissionDescription_WhenTooLong_ReturnsInvalidDescription()
    {
        var tooLong = new string('a', MissionDescription.MaxLength + 1);
        MissionDescription.Create(tooLong).Error.Should().Be(MissionErrors.InvalidDescription);
    }

    // ── SessionDuration ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SessionDuration_WhenNonPositive_ReturnsInvalidMaxDuration(int minutes)
    {
        SessionDuration.Create(minutes).Error.Should().Be(MissionErrors.InvalidMaxDuration);
    }

    [Fact]
    public void SessionDuration_WhenValid_StoresMinutes()
    {
        SessionDuration.Create(60).Value.Minutes.Should().Be(60);
    }
}
