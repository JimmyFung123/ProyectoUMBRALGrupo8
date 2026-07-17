namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions.Queries.GetSessionDashboard;
using SessionService.Domain.Sessions;
using Xunit;

public class GetSessionDashboardQueryHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ISessionEventRepository> _eventRepoMock = new();
    private readonly GetSessionDashboardQueryHandler _handler;

    public GetSessionDashboardQueryHandlerTests()
    {
        _handler = new GetSessionDashboardQueryHandler(
            _sessionRepoMock.Object,
            _eventRepoMock.Object);
    }

    // ── Sesión no encontrada ──────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionNotFound_ReturnsNotFoundError()
    {
        var sessionId = Guid.NewGuid();

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new GetSessionDashboardQuery(sessionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    // ── Sin eventos ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionHasNoEvents_ReturnsEmptyLog()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión Vacía").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _eventRepoMock
            .Setup(r => r.GetRecentBySessionIdAsync(sessionId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent>());

        var result = await _handler.Handle(new GetSessionDashboardQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecentEvents.Should().BeEmpty();
        result.Value.Name.Should().Be("Sesión Vacía");
        result.Value.Status.Should().Be("Pending");
    }

    // ── Mapeo de eventos ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionHasEvents_ReturnsMappedEventDtos()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión con Eventos").Value;

        var event1 = SessionEvent.Create(sessionId, "La sesión fue iniciada");
        var event2 = SessionEvent.Create(sessionId, "Equipo Alfa resolvió la etapa 1");

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _eventRepoMock
            .Setup(r => r.GetRecentBySessionIdAsync(sessionId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent> { event1, event2 });

        var result = await _handler.Handle(new GetSessionDashboardQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecentEvents.Should().HaveCount(2);
        result.Value.RecentEvents.Should().Contain(e => e.Description == "La sesión fue iniciada");
        result.Value.RecentEvents.Should().Contain(e => e.Description == "Equipo Alfa resolvió la etapa 1");
    }

    // ── Límite de eventos consultados ─────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCalled_AlwaysRequests20MostRecentEvents()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión Activa").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _eventRepoMock
            .Setup(r => r.GetRecentBySessionIdAsync(sessionId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent>());

        await _handler.Handle(new GetSessionDashboardQuery(sessionId), default);

        _eventRepoMock.Verify(
            r => r.GetRecentBySessionIdAsync(sessionId, 20, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Mapeo de campos de sesión ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionExists_MapsSessionFieldsCorrectly()
    {
        var missionId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var scheduled = DateTime.UtcNow.AddDays(1);
        var session = Session.Create(missionId, "Operación Fénix", scheduled).Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _eventRepoMock
            .Setup(r => r.GetRecentBySessionIdAsync(sessionId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent>());

        var result = await _handler.Handle(new GetSessionDashboardQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Operación Fénix");
        result.Value.Status.Should().Be("Pending");
        result.Value.ScheduledAt.Should().BeCloseTo(scheduled, precision: TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Handle_WhenSessionHasNoScheduledAt_ReturnsDtoWithNullScheduledAt()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sin Programar").Value;

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _eventRepoMock
            .Setup(r => r.GetRecentBySessionIdAsync(sessionId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent>());

        var result = await _handler.Handle(new GetSessionDashboardQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.ScheduledAt.Should().BeNull();
    }

    // ── Mapeo de campos de evento ─────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenSessionHasEvents_MapsEventFieldsCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión Detallada").Value;
        var evt = SessionEvent.Create(sessionId, "Pista enviada al Equipo Alfa");

        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _eventRepoMock
            .Setup(r => r.GetRecentBySessionIdAsync(sessionId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionEvent> { evt });

        var result = await _handler.Handle(new GetSessionDashboardQuery(sessionId), default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.RecentEvents.Single();
        dto.Id.Should().Be(evt.Id);
        dto.Description.Should().Be(evt.Description);
        dto.OccurredAt.Should().BeCloseTo(evt.OccurredAt, precision: TimeSpan.FromSeconds(1));
    }
}
