namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using UMBRAL_Back_end.Domain.Sessions;
using Xunit;

public class SessionTests
{
    // ── Success cases ─────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidInputs_ReturnsSession()
    {
        var missionId = Guid.NewGuid();

        var result = Session.Create(missionId, "Round 1", null);

        result.IsSuccess.Should().BeTrue();
        result.Value.MissionId.Should().Be(missionId);
        result.Value.Name.Should().Be("Round 1");
        result.Value.Status.Should().Be(SessionStatus.Pending);
        result.Value.ScheduledAt.Should().BeNull();
        result.Value.Id.Should().NotBeEmpty();
        result.Value.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithScheduledAt_StoresDate()
    {
        var scheduledAt = DateTime.UtcNow.AddDays(7);

        var result = Session.Create(Guid.NewGuid(), "Scheduled Session", scheduledAt);

        result.IsSuccess.Should().BeTrue();
        result.Value.ScheduledAt.Should().Be(scheduledAt);
    }

    [Fact]
    public void Create_TrimsName()
    {
        var result = Session.Create(Guid.NewGuid(), "  Padded Name  ", null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Padded Name");
    }

    // ── Validation failures ───────────────────────────────────────────────────

    [Fact]
    public void Create_EmptyName_ReturnsError()
    {
        var result = Session.Create(Guid.NewGuid(), "", null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.InvalidName);
    }

    [Fact]
    public void Create_WhitespaceName_ReturnsError()
    {
        var result = Session.Create(Guid.NewGuid(), "   ", null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.InvalidName);
    }

    [Fact]
    public void Create_EmptyMissionId_ReturnsError()
    {
        var result = Session.Create(Guid.Empty, "Valid Name", null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.MissionRequired);
    }

    // ── Status default ────────────────────────────────────────────────────────

    [Fact]
    public void Create_StatusIsPendingByDefault()
    {
        var result = Session.Create(Guid.NewGuid(), "Test Session", null);

        result.Value.Status.Should().Be(SessionStatus.Pending);
    }
}
