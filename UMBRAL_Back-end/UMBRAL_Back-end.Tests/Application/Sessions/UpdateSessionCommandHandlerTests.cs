namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application;
using SessionService.Application.Sessions.Commands.UpdateSession;
using SessionService.Domain.Sessions;
using SessionService.Domain.Sessions.Events;
using Xunit;

public class UpdateSessionCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly UpdateSessionCommandHandler _handler;

    public UpdateSessionCommandHandlerTests()
    {
        _handler = new UpdateSessionCommandHandler(_sessionRepoMock.Object);
    }

    // ── Flujo feliz ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionIsPending_UpdatesNameAndScheduledAt()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Nombre original").Value;
        var newScheduled = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Unspecified);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(
            new UpdateSessionCommand(sessionId, "Nombre actualizado", newScheduled), default);

        result.IsSuccess.Should().BeTrue();
        session.Name.Should().Be("Nombre actualizado");
        session.ScheduledAt!.Value.Kind.Should().Be(DateTimeKind.Utc);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenScheduledAtIsNull_ClearsScheduledDate()
    {
        var sessionId = Guid.NewGuid();
        var scheduled = new DateTime(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);
        var session = Session.Create(Guid.NewGuid(), "Sesión programada", scheduled).Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(
            new UpdateSessionCommand(sessionId, "Sesión sin fecha", null), default);

        result.IsSuccess.Should().BeTrue();
        session.ScheduledAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenValid_RaisesSessionUpdatedDomainEventWithOperator()
    {
        // HU-26: editing a session must leave an immutable trail.
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Nombre original").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(
            new UpdateSessionCommand(sessionId, "Nombre actualizado", null, "Prof. Ortega"),
            default);

        result.IsSuccess.Should().BeTrue();
        var raised = session.DomainEvents.OfType<SessionUpdatedDomainEvent>().Should().ContainSingle().Which;
        raised.OperatorName.Should().Be("Prof. Ortega");
        raised.Name.Should().Be("Nombre actualizado");
    }

    // ── Sesión no encontrada ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        var sessionId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(
            new UpdateSessionCommand(sessionId, "Nuevo nombre", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Estado no editable ────────────────────────────────────────────────────

    [Theory]
    [InlineData(SessionStatus.InProgress)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task Handle_WhenSessionIsNotPending_ReturnsCannotEditError(SessionStatus status)
    {
        var sessionId = Guid.NewGuid();
        var session = CreateSessionWithStatus(status);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(
            new UpdateSessionCommand(sessionId, "Nuevo nombre", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotEditNonPendingSession);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Nombre vacío ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenNameIsEmpty_ReturnsInvalidNameError()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Nombre válido").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(
            new UpdateSessionCommand(sessionId, "   ", null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.InvalidName);
        _sessionRepoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a Session in a non-Pending state using reflection,
    /// since state transitions belong to later user stories (HU-11).
    /// </summary>
    private static Session CreateSessionWithStatus(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión de prueba").Value;
        typeof(Session)
            .GetProperty(nameof(Session.Status))!
            .SetValue(session, status);
        return session;
    }
}
