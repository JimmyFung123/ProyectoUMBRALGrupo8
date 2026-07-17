namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using StageService.Domain.Stages;
using Xunit;

/// <summary>
/// Stage domain (v2): value objects + RB-20 enforcement in Stage.Create/Update.
/// </summary>
public class StageValueObjectsTests
{
    // ── StageOrder / ScoreValue / GeoPoint / QrCodeValue ─────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void StageOrder_WhenNonPositive_Fails(int order)
        => StageOrder.Create(order).Error.Should().Be(StageErrors.InvalidOrder);

    [Fact]
    public void StageOrder_WhenPositive_Succeeds()
        => StageOrder.Create(3).Value.Value.Should().Be(3);

    [Fact]
    public void ScoreValue_WhenNegative_Fails()
        => ScoreValue.Create(-5).Error.Should().Be(StageErrors.InvalidBaseScore);

    [Fact]
    public void ScoreValue_WhenZeroOrMore_Succeeds()
        => ScoreValue.Create(0).Value.Points.Should().Be(0);

    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public void GeoPoint_WhenOutOfRange_Fails(double lat, double lng)
        => GeoPoint.Create(lat, lng).Error.Should().Be(StageErrors.InvalidCoordinates);

    [Fact]
    public void GeoPoint_WhenValid_Succeeds()
    {
        var p = GeoPoint.Create(10.5, -66.9).Value;
        p.Latitude.Should().Be(10.5);
        p.Longitude.Should().Be(-66.9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void QrCodeValue_WhenBlank_Fails(string? raw)
        => QrCodeValue.Create(raw).Error.Should().Be(StageErrors.QrCodeRequired);

    [Fact]
    public void QrCodeValue_TrimsValue()
        => QrCodeValue.Create("  QR-1 ").Value.Value.Should().Be("QR-1");

    // ── RB-20 enforcement at the aggregate level ─────────────────────────────

    [Fact]
    public void Create_TreasureHunt_WithoutCoordinates_ReturnsCoordinatesRequired()
    {
        var result = Stage.Create(Guid.NewGuid(), "Tesoro", StageType.TreasureHunt, 1, 100,
            question: null, latitude: null, longitude: null, qrCode: "QR-1");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.CoordinatesRequired);
    }

    [Fact]
    public void Create_TreasureHunt_WithoutQrCode_ReturnsQrCodeRequired()
    {
        var result = Stage.Create(Guid.NewGuid(), "Tesoro", StageType.TreasureHunt, 1, 100,
            question: null, latitude: 10.5, longitude: -66.9, qrCode: null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.QrCodeRequired);
    }

    [Fact]
    public void Create_TreasureHunt_WithGeoAndQr_Succeeds()
    {
        var result = Stage.Create(Guid.NewGuid(), "Tesoro", StageType.TreasureHunt, 1, 100,
            question: null, latitude: 10.5, longitude: -66.9, qrCode: "QR-1");

        result.IsSuccess.Should().BeTrue();
        result.Value.QrCode.Should().Be("QR-1");
    }

    [Fact]
    public void Create_Trivia_DoesNotRequireGeoOrQr()
    {
        var result = Stage.Create(Guid.NewGuid(), "Trivia", StageType.Trivia, 1, 100,
            question: "¿?", latitude: null, longitude: null, qrCode: null);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithInvalidOrder_ReturnsInvalidOrder()
    {
        var result = Stage.Create(Guid.NewGuid(), "Trivia", StageType.Trivia, 0, 100);
        result.Error.Should().Be(StageErrors.InvalidOrder);
    }

    [Fact]
    public void Create_WithNegativeScore_ReturnsInvalidBaseScore()
    {
        var result = Stage.Create(Guid.NewGuid(), "Trivia", StageType.Trivia, 1, -10);
        result.Error.Should().Be(StageErrors.InvalidBaseScore);
    }

    // ── Update now enforces the same invariants and returns Result<bool> ─────

    [Fact]
    public void Update_TreasureHunt_RemovingQr_Fails()
    {
        var stage = Stage.Create(Guid.NewGuid(), "Tesoro", StageType.TreasureHunt, 1, 100,
            question: null, latitude: 10.5, longitude: -66.9, qrCode: "QR-1").Value;

        var result = stage.Update("Tesoro", 1, 100, null, 10.5, -66.9, qrCode: null, null, null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.QrCodeRequired);
        stage.QrCode.Should().Be("QR-1"); // unchanged on failure
    }

    [Fact]
    public void Update_Valid_AppliesChanges()
    {
        var stage = Stage.Create(Guid.NewGuid(), "Original", StageType.Trivia, 1, 50).Value;

        var result = stage.Update("Nuevo", 2, 80, "¿?", null, null, null, null, null);

        result.IsSuccess.Should().BeTrue();
        stage.Title.Should().Be("Nuevo");
        stage.Order.Should().Be(2);
        stage.BaseScore.Should().Be(80);
    }
}
