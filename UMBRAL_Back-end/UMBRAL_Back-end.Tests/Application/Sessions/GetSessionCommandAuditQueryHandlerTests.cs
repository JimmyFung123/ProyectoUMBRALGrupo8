namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Queries.GetSessionCommandAudit;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// HU-26 — technical command audit log query handler.
/// </summary>
public class GetSessionCommandAuditQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly GetSessionCommandAuditQueryHandler _handler;

    public GetSessionCommandAuditQueryHandlerTests()
    {
        _handler = new GetSessionCommandAuditQueryHandler(
            _sessionRepoMock.Object,
            _eventRepoMock.Object);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        var sessionId = Guid.NewGuid();
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new GetSessionCommandAuditQuery(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    [Fact]
    public async Task Handle_MapsCommandTypeAndOutcome_PreservingChronologicalOrder()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión auditada").Value;

        var first = SessionEvent.Create(
            sessionId,
            "Se creó la sesión 'Sesión auditada'.",
            actorName: "Prof. Ortega",
            commandType: "CreateSessionCommand",
            outcome: SessionEvent.OutcomeSuccess);

        Thread.Sleep(5);

        var second = SessionEvent.Create(
            sessionId,
            "El equipo 'Alfa' respondió incorrectamente la etapa 1.",
            actorName: "Equipo Alfa",
            commandType: "SubmitTriviaAnswerCommand",
            outcome: SessionEvent.OutcomeFailure);

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _eventRepoMock
            .Setup(r => r.GetAllBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent> { first, second });

        var result = await _handler.Handle(new GetSessionCommandAuditQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.SessionId.Should().Be(session.Id);
        result.Value.SessionStatus.Should().Be("Pending");
        result.Value.Entries.Should().HaveCount(2);

        result.Value.Entries[0].CommandType.Should().Be("CreateSessionCommand");
        result.Value.Entries[0].Outcome.Should().Be(SessionEvent.OutcomeSuccess);
        result.Value.Entries[0].ActorName.Should().Be("Prof. Ortega");

        result.Value.Entries[1].CommandType.Should().Be("SubmitTriviaAnswerCommand");
        result.Value.Entries[1].Outcome.Should().Be(SessionEvent.OutcomeFailure);

        // Millisecond-precise timestamps are preserved (HU-26 criterion 1).
        result.Value.Entries[1].OccurredAt.Should().BeAfter(result.Value.Entries[0].OccurredAt);
    }

    [Fact]
    public async Task Handle_WhenSessionHasNoEvents_ReturnsEmptyEntries()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sin actividad").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _eventRepoMock
            .Setup(r => r.GetAllBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent>());

        var result = await _handler.Handle(new GetSessionCommandAuditQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().BeEmpty();
    }
}
