namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.BroadcastOperatorMessage;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// HU-28 — operator broadcast message handler.
/// </summary>
public class BroadcastOperatorMessageCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepo = new();
    private readonly Mock<ISessionEventRepository> _eventRepo = new();
    private readonly Mock<ISessionNotifier> _notifierMock = new();
    private readonly BroadcastOperatorMessageCommandHandler _handler;

    public BroadcastOperatorMessageCommandHandlerTests()
    {
        _handler = new BroadcastOperatorMessageCommandHandler(
            _sessionRepo.Object, _eventRepo.Object, _notifierMock.Object);
    }

    private Session BuildSession(SessionStatus status)
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión de prueba").Value;
        if (status == SessionStatus.InProgress) session.Start();
        if (status == SessionStatus.Paused) { session.Start(); session.Pause(); }
        if (status == SessionStatus.Completed) { session.Start(); session.Finalize(); }
        return session;
    }

    [Fact]
    public async Task Handle_EmptyMessage_ReturnsValidationError()
    {
        var result = await _handler.Handle(
            new BroadcastOperatorMessageCommand(Guid.NewGuid(), "   ", "Prof. Ortega"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.EmptyBroadcastMessage);
    }

    [Fact]
    public async Task Handle_TooLongMessage_ReturnsValidationError()
    {
        var long_ = new string('a', 241);
        var result = await _handler.Handle(
            new BroadcastOperatorMessageCommand(Guid.NewGuid(), long_, "Prof. Ortega"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.BroadcastMessageTooLong);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsNotFound()
    {
        _sessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(
            new BroadcastOperatorMessageCommand(Guid.NewGuid(), "Hola equipos!", "Prof. Ortega"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    [Theory]
    [InlineData(SessionStatus.Pending)]
    [InlineData(SessionStatus.Completed)]
    [InlineData(SessionStatus.Cancelled)]
    public async Task Handle_InvalidStatus_RejectsBroadcast(SessionStatus status)
    {
        if (status == SessionStatus.Cancelled)
        {
            // Build a Pending session and skip the transition we don't have here
            var session = Session.Create(Guid.NewGuid(), "Test").Value;
            _sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
            // Pending is also not allowed, which is what we're testing
        }
        else
        {
            var session = BuildSession(status);
            _sessionRepo
                .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(session);
        }

        // Either way, the status is not InProgress/Paused so the broadcast must
        // be rejected with the dedicated error code.
        var anyId = Guid.NewGuid();
        var anySession = status == SessionStatus.Cancelled
            ? Session.Create(Guid.NewGuid(), "Test").Value
            : BuildSession(status);

        _sessionRepo
            .Setup(r => r.GetByIdAsync(It.Is<Guid>(g => g != Guid.Empty), It.IsAny<CancellationToken>()))
            .ReturnsAsync(anySession);

        var result = await _handler.Handle(
            new BroadcastOperatorMessageCommand(anyId, "Mensaje", "Prof. Ortega"),
            default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.CannotBroadcastMessage);
    }

    [Fact]
    public async Task Handle_InProgress_BroadcastsAndAuditLogs()
    {
        var session = BuildSession(SessionStatus.InProgress);
        _sessionRepo
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(
            new BroadcastOperatorMessageCommand(session.Id, "  ¡Vamos equipos! ", "Prof. Ortega"),
            default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Message.Should().Be("¡Vamos equipos!"); // trimmed

        _notifierMock.Verify(n => n.NotifyOperatorMessageAsync(
            session.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);

        // Audit row was persisted with the operator's name.
        _eventRepo.Verify(
            r => r.AddAsync(
                It.Is<SessionEvent>(e =>
                    e.SessionId == session.Id
                    && e.ActorName == "Prof. Ortega"
                    && e.CommandType == nameof(BroadcastOperatorMessageCommand)
                    && e.Outcome == SessionEvent.OutcomeSuccess),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Paused_AlsoBroadcasts()
    {
        var session = BuildSession(SessionStatus.Paused);
        _sessionRepo
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(
            new BroadcastOperatorMessageCommand(session.Id, "Pausa para tomar agua", "Prof. Ortega"),
            default);

        result.IsSuccess.Should().BeTrue();
        _notifierMock.Verify(n => n.NotifyOperatorMessageAsync(
            session.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingOperatorName_FallsBackToSistemaActor()
    {
        var session = BuildSession(SessionStatus.InProgress);
        _sessionRepo
            .Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        await _handler.Handle(
            new BroadcastOperatorMessageCommand(session.Id, "Hola", null),
            default);

        _eventRepo.Verify(
            r => r.AddAsync(
                It.Is<SessionEvent>(e => e.ActorName == SessionEvent.SystemActor),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
