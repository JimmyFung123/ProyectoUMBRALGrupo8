namespace UMBRAL_Back_end.Tests.Domain;

using ClueService.Domain.Clues;
using FluentAssertions;
using Xunit;

/// <summary>
/// ClueService domain (v2): GeoPoint / GeoRadius value objects + that
/// Clue.Create routes treasure validation through them (RB-21).
/// </summary>
public class ClueValueObjectsTests
{
    // ── GeoPoint ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, -66.0)]
    [InlineData(10.0, null)]
    [InlineData(91.0, 0.0)]
    [InlineData(0.0, 181.0)]
    public void GeoPoint_WhenInvalid_FailsWithInvalidGeoData(double? lat, double? lng)
        => GeoPoint.Create(lat, lng).Error.Should().Be(ClueErrors.InvalidGeoData);

    [Fact]
    public void GeoPoint_WhenValid_Succeeds()
    {
        var p = GeoPoint.Create(10.49, -66.85).Value;
        p.Latitude.Should().Be(10.49);
        p.Longitude.Should().Be(-66.85);
    }

    // ── GeoRadius ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(-50)]
    public void GeoRadius_WhenNonPositive_FailsWithInvalidGeoData(int? meters)
        => GeoRadius.Create(meters).Error.Should().Be(ClueErrors.InvalidGeoData);

    [Fact]
    public void GeoRadius_WhenPositive_Succeeds()
        => GeoRadius.Create(75).Value.Meters.Should().Be(75);

    // ── Clue.Create wiring (RB-21) ───────────────────────────────────────────

    [Fact]
    public void Create_TreasureHunt_WithValidGeo_Succeeds()
    {
        var result = Clue.Create(Guid.NewGuid(), Guid.NewGuid(), "TreasureHunt", 1,
            content: null, latitude: 10.49, longitude: -66.85, radiusMeters: 50);

        result.IsSuccess.Should().BeTrue();
        result.Value.RadiusMeters.Should().Be(50);
    }

    [Fact]
    public void Create_TreasureHunt_WithMissingRadius_FailsWithInvalidGeoData()
    {
        var result = Clue.Create(Guid.NewGuid(), Guid.NewGuid(), "TreasureHunt", 1,
            content: null, latitude: 10.49, longitude: -66.85, radiusMeters: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.InvalidGeoData);
    }

    [Fact]
    public void Create_TreasureHunt_WithMissingCoordinates_FailsWithInvalidGeoData()
    {
        var result = Clue.Create(Guid.NewGuid(), Guid.NewGuid(), "TreasureHunt", 1,
            content: null, latitude: null, longitude: null, radiusMeters: 50);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ClueErrors.InvalidGeoData);
    }

    [Fact]
    public void Create_Trivia_WithContent_Succeeds()
    {
        var result = Clue.Create(Guid.NewGuid(), Guid.NewGuid(), "Trivia", 1,
            content: "Busca cerca del árbol", latitude: null, longitude: null, radiusMeters: null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Content.Should().Be("Busca cerca del árbol");
    }
}
