namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Queries.GetSessionAudit;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// HU-22: full audit timeline for a session.
/// </summary>
public class GetSessionAuditQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly GetSessionAuditQueryHandler _handler;

    public GetSessionAuditQueryHandlerTests()
    {
        _handler = new GetSessionAuditQueryHandler(
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

        var result = await _handler.Handle(new GetSessionAuditQuery(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenSessionHasNoEvents_ReturnsEmptyTimeline()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sin eventos").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _eventRepoMock
            .Setup(r => r.GetAllBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent>());

        var result = await _handler.Handle(new GetSessionAuditQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().BeEmpty();
        result.Value.SessionStatus.Should().Be("Pending");
        result.Value.SessionId.Should().Be(session.Id);
    }

    [Fact]
    public async Task Handle_MapsAllFields_IncludingActorName()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Test").Value;

        var e1 = SessionEvent.Create(sessionId, "La sesión fue iniciada.", actorName: "Prof. Ortega");
        var e2 = SessionEvent.Create(sessionId, "Pista #1 liberada automáticamente al equipo 'Alfa'.");
        var e3 = SessionEvent.Create(sessionId, "El equipo 'Alfa' resolvió la etapa 1 escaneando el código QR.", actorName: "Equipo Alfa");

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _eventRepoMock
            .Setup(r => r.GetAllBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent> { e1, e2, e3 });

        var result = await _handler.Handle(new GetSessionAuditQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Entries.Should().HaveCount(3);
        result.Value.Entries[0].ActorName.Should().Be("Prof. Ortega");
        result.Value.Entries[1].ActorName.Should().Be("Sistema"); // default
        result.Value.Entries[2].ActorName.Should().Be("Equipo Alfa");
        result.Value.Entries[0].Description.Should().Contain("iniciada");
    }

    [Fact]
    public async Task Handle_PreservesRepositoryOrder()
    {
        // The repository returns events oldest-first. The handler must not re-sort.
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Orden").Value;

        var oldest = SessionEvent.Create(sessionId, "Primero");
        Thread.Sleep(5);
        var middle = SessionEvent.Create(sessionId, "Segundo");
        Thread.Sleep(5);
        var newest = SessionEvent.Create(sessionId, "Tercero");

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _eventRepoMock
            .Setup(r => r.GetAllBySessionIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent> { oldest, middle, newest });

        var result = await _handler.Handle(new GetSessionAuditQuery(sessionId), default);

        result.Value.Entries.Select(e => e.Description)
            .Should().Equal("Primero", "Segundo", "Tercero");
    }
}
