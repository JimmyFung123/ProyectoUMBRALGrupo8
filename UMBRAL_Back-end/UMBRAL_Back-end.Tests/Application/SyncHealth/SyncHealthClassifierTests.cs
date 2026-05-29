namespace UMBRAL_Back_end.Tests.Application.SyncHealth;

using FluentAssertions;
using SessionService.Application.SyncHealth;
using Xunit;

/// <summary>
/// HU-27 — drift is the only signal that flips the status to Critical. Wall-
/// clock lag is informational only: an idle system is healthy by definition.
/// </summary>
public class SyncHealthClassifierTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(120)]
    [InlineData(7922)]
    public void Classify_NoDrift_IsHealthyRegardlessOfLag(int lag)
    {
        SyncHealthClassifier.Classify(lag, hasDrift: false).Should().Be("Healthy");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(120)]
    public void Classify_WithDrift_AlwaysCritical(int lag)
    {
        SyncHealthClassifier.Classify(lag, hasDrift: true).Should().Be("Critical");
    }

    [Fact]
    public void Classify_NoTimestampNoDrift_IsHealthy()
    {
        SyncHealthClassifier.Classify(lagSeconds: null, hasDrift: false).Should().Be("Healthy");
    }

    [Fact]
    public void Classify_NoTimestampWithDrift_IsCritical()
    {
        SyncHealthClassifier.Classify(lagSeconds: null, hasDrift: true).Should().Be("Critical");
    }

    [Fact]
    public void ComputeLagSeconds_NullLastUpdated_ReturnsNull()
    {
        SyncHealthClassifier.ComputeLagSeconds(null, DateTime.UtcNow).Should().BeNull();
    }

    [Fact]
    public void ComputeLagSeconds_PastTimestamp_ReturnsPositiveSeconds()
    {
        var now = new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);
        var lastUpdated = now.AddSeconds(-15);
        SyncHealthClassifier.ComputeLagSeconds(lastUpdated, now).Should().Be(15);
    }

    [Fact]
    public void ComputeLagSeconds_FutureTimestamp_ClampedToZero()
    {
        var now = new DateTime(2026, 5, 29, 12, 0, 0, DateTimeKind.Utc);
        var lastUpdated = now.AddSeconds(5);
        SyncHealthClassifier.ComputeLagSeconds(lastUpdated, now).Should().Be(0);
    }
}
