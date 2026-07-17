namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// Unit tests for Session state-machine methods: Start, Pause, Resume, Finalize.
/// HU-11: Gestionar estados de la sesión.
/// </summary>
public class SessionStateTests
{
    // ── Helper ────────────────────────────────────────────────────────────────

    private static Session CreateWithStatus(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), "Test session").Value;
        typeof(Session)
            .GetProperty(nameof(Session.Status))!
            .SetValue(session, status);
        return session;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Start()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Start_WhenPending_ReturnsSuccessAndSetsInProgress()
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión").Value;

        var result = session.Start();

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.InProgress);
    }

    [Theory]
    [InlineData(SessionStatus.InProgress)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public void Start_WhenNotPending_ReturnsCannotStartError(SessionStatus status)
    {
        var session = CreateWithStatus(status);

        var result = session.Start();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotStartSession);
        session.Status.Should().Be(status); // status unchanged
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Pause()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Pause_WhenInProgress_ReturnsSuccessAndSetsPaused()
    {
        var session = CreateWithStatus(SessionStatus.InProgress);

        var result = session.Pause();

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Paused);
    }

    [Theory]
    [InlineData(SessionStatus.Pending)]
    [InlineData(SessionStatus.Paused)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public void Pause_WhenNotInProgress_ReturnsCannotPauseError(SessionStatus status)
    {
        var session = CreateWithStatus(status);

        var result = session.Pause();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotPauseSession);
        session.Status.Should().Be(status);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Resume()
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Resume_WhenPaused_ReturnsSuccessAndSetsInProgress()
    {
        var session = CreateWithStatus(SessionStatus.Paused);

        var result = session.Resume();

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.InProgress);
    }

    [Theory]
    [InlineData(SessionStatus.Pending)]
    [InlineData(SessionStatus.InProgress)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public void Resume_WhenNotPaused_ReturnsCannotResumeError(SessionStatus status)
    {
        var session = CreateWithStatus(status);

        var result = session.Resume();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotResumeSession);
        session.Status.Should().Be(status);
    }

    // ── Verify Finalized cannot be resumed (irreversibility) ─────────────────

    [Fact]
    public void Resume_WhenCompleted_IsIrreversible()
    {
        var session = CreateWithStatus(SessionStatus.Completed);

        var result = session.Resume();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotResumeSession);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Finalize()
    // ══════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(SessionStatus.InProgress)]
    [InlineData(SessionStatus.Paused)]
    public void Finalize_WhenActiveOrPaused_ReturnsSuccessAndSetsCompleted(SessionStatus status)
    {
        var session = CreateWithStatus(status);

        var result = session.Finalize();

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Completed);
    }

    [Theory]
    [InlineData(SessionStatus.Pending)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public void Finalize_WhenNotActiveOrPaused_ReturnsCannotFinalizeError(SessionStatus status)
    {
        var session = CreateWithStatus(status);

        var result = session.Finalize();

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotFinalizeSession);
        session.Status.Should().Be(status);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Full happy-path transitions
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FullLifecycle_PendingToCompletedViaInProgress()
    {
        var session = Session.Create(Guid.NewGuid(), "Ciclo completo").Value;

        session.Start().IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.InProgress);

        session.Finalize().IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public void FullLifecycle_PendingToCompletedViaPause()
    {
        var session = Session.Create(Guid.NewGuid(), "Ciclo con pausa").Value;

        session.Start().IsSuccess.Should().BeTrue();
        session.Pause().IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Paused);

        session.Resume().IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.InProgress);

        session.Finalize().IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public void FullLifecycle_FinalizeFromPausedDirectly()
    {
        var session = Session.Create(Guid.NewGuid(), "Finalizar desde pausa").Value;

        session.Start();
        session.Pause();

        var result = session.Finalize();

        result.IsSuccess.Should().BeTrue();
        session.Status.Should().Be(SessionStatus.Completed);
    }
}
