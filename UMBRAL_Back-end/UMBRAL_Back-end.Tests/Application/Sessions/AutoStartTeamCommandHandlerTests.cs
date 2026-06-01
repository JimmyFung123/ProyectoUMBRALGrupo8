namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.AutoStartTeam;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// Pruebas del comando de auto-arranque (extraido de la fachada por SRP/CQRS).
/// Cubren todas las ramas: arranca solo cuando la sesion esta en curso y el equipo
/// esta en orden 0; en cualquier otro caso es un no-op tolerante que devuelve exito.
/// </summary>
public class AutoStartTeamCommandHandlerTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly AutoStartTeamCommandHandler _handler;

    public AutoStartTeamCommandHandlerTests()
    {
        _handler = new AutoStartTeamCommandHandler(_sessionRepoMock.Object, _teamClientMock.Object);
    }

    private Session SetupSession(bool inProgress)
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión").Value;
        if (inProgress) session.Start();
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        return session;
    }

    [Fact]
    public async Task Handle_WhenInProgressAndTeamAtZero_AdvancesToStageOne()
    {
        var teamId = Guid.NewGuid();
        SetupSession(inProgress: true);
        _teamClientMock
            .Setup(c => c.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInfoItem(teamId, "Equipo", 0));
        _teamClientMock
            .Setup(c => c.ForceAdvanceTeamAsync(teamId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageTransitionResult?)null);

        var result = await _handler.Handle(new AutoStartTeamCommand(Guid.NewGuid(), teamId), default);

        result.IsSuccess.Should().BeTrue();
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(teamId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTeamAlreadyStarted_DoesNothing()
    {
        var teamId = Guid.NewGuid();
        SetupSession(inProgress: true);
        _teamClientMock
            .Setup(c => c.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInfoItem(teamId, "Equipo", 2)); // ya en una etapa

        var result = await _handler.Handle(new AutoStartTeamCommand(Guid.NewGuid(), teamId), default);

        result.IsSuccess.Should().BeTrue();
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionNotInProgress_DoesNotTouchTeamService()
    {
        var teamId = Guid.NewGuid();
        SetupSession(inProgress: false); // Pending

        var result = await _handler.Handle(new AutoStartTeamCommand(Guid.NewGuid(), teamId), default);

        result.IsSuccess.Should().BeTrue();
        // Mientras no este en curso, ni siquiera se consulta a TeamService (ahorra un HTTP por poll).
        _teamClientMock.Verify(
            c => c.GetTeamByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenSessionNotFound_DoesNothing_ReturnsSuccess()
    {
        var teamId = Guid.NewGuid();
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new AutoStartTeamCommand(Guid.NewGuid(), teamId), default);

        result.IsSuccess.Should().BeTrue();
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTeamNotFound_DoesNothing_ReturnsSuccess()
    {
        var teamId = Guid.NewGuid();
        SetupSession(inProgress: true);
        _teamClientMock
            .Setup(c => c.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamInfoItem?)null);

        var result = await _handler.Handle(new AutoStartTeamCommand(Guid.NewGuid(), teamId), default);

        result.IsSuccess.Should().BeTrue();
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
