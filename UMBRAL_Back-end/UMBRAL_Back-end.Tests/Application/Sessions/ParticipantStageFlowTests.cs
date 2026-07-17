namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Commands.AutoStartTeam;
using SessionService.Application.Sessions.Facade;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>
/// Pruebas de FLUJO del endpoint participant-stage tras mover el auto-arranque al
/// lado de comandos (F2). Reproducen lo que hace el controller: enviar
/// <see cref="AutoStartTeamCommand"/> (escritura) y LUEGO el query de lectura pura.
///
/// Se usa un fake de TeamService CON ESTADO: ForceAdvance muta el orden y GetTeamById
/// lo refleja, igual que la consistencia lectura-tras-escritura real de TeamService
/// (mismo ITeamRepository + SaveChanges sincrono). Asi se prueba que el avance del
/// comando "se refleja" en el query posterior, y que la fachada SOLA ya no arranca.
/// </summary>
public class ParticipantStageFlowTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IStageServiceClient> _stageClientMock = new();

    private readonly AutoStartTeamCommandHandler _autoStart;
    private readonly ParticipantStageFacade _facade;

    // Estado compartido del equipo: arranca en 0 y ForceAdvance lo lleva a 1.
    private int _teamOrder;

    public ParticipantStageFlowTests()
    {
        _autoStart = new AutoStartTeamCommandHandler(_sessionRepoMock.Object, _teamClientMock.Object);
        _facade = new ParticipantStageFacade(
            _sessionRepoMock.Object, _teamClientMock.Object, _stageClientMock.Object);
    }

    private void SetupInProgressSession()
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión").Value;
        session.Start();
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
    }

    private void SetupStatefulTeam(Guid teamId)
    {
        // GetTeamById refleja SIEMPRE el orden actual (lambda evaluada en cada llamada).
        _teamClientMock
            .Setup(c => c.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new TeamInfoItem(teamId, "Equipo Alfa", _teamOrder));
        // ForceAdvance a 1 muta el estado, como haria TeamService al persistir.
        _teamClientMock
            .Setup(c => c.ForceAdvanceTeamAsync(teamId, 1, It.IsAny<CancellationToken>()))
            .Callback(() => _teamOrder = 1)
            .ReturnsAsync((StageTransitionResult?)null);
    }

    [Fact]
    public async Task Flow_CommandThenQuery_AutoStartsTeam_AndStageOneIsReflected()
    {
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var stage1Id = Guid.NewGuid();
        _teamOrder = 0; // equipo recién inscrito, aún no entra a ninguna etapa

        SetupInProgressSession();
        SetupStatefulTeam(teamId);
        _stageClientMock
            .Setup(c => c.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageInfo> { new(stage1Id, 1), new(Guid.NewGuid(), 2) });
        _stageClientMock
            .Setup(c => c.GetStageWithOptionsAsync(stage1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(
                stage1Id, "Etapa 1", "Trivia", 1, 100, "¿?",
                new List<TriviaOptionInfo> { new(Guid.NewGuid(), "A", IsCorrect: true) }));

        // 1) El controller envía el comando de auto-arranque (escritura).
        var commandResult = await _autoStart.Handle(new AutoStartTeamCommand(sessionId, teamId), default);
        commandResult.IsSuccess.Should().BeTrue();

        // 2) Y luego el query de lectura pura.
        var queryResult = await _facade.GetCurrentStageAsync(sessionId, teamId, default);

        // El avance del comando se refleja en el query: el equipo ya está en la etapa 1.
        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value.StageId.Should().Be(stage1Id);
        queryResult.Value.Title.Should().Be("Etapa 1");
        queryResult.Value.CurrentStageOrder.Should().Be(1);
        // IsCorrect nunca sale al participante.
        queryResult.Value.Options.Should().ContainSingle();

        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(teamId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Flow_QueryWithoutCommand_DoesNotAutoStart_StaysWaiting()
    {
        // Sin el comando previo, la fachada SOLA no arranca: el equipo sigue en 0 → Waiting.
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        _teamOrder = 0;

        SetupInProgressSession();
        SetupStatefulTeam(teamId);
        _stageClientMock
            .Setup(c => c.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageInfo> { new(Guid.NewGuid(), 1), new(Guid.NewGuid(), 2) });

        var queryResult = await _facade.GetCurrentStageAsync(sessionId, teamId, default);

        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value.Title.Should().Be("Waiting");
        queryResult.Value.CurrentStageOrder.Should().Be(0);
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Flow_SecondPoll_CommandIsNoOp_AndStageStillReflected()
    {
        // Segundo poll: el equipo ya está en 1; el comando es no-op (no vuelve a avanzar)
        // y el query sigue devolviendo la etapa 1. Idempotencia del flujo.
        var sessionId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var stage1Id = Guid.NewGuid();
        _teamOrder = 1; // ya arrancado en un poll anterior

        SetupInProgressSession();
        SetupStatefulTeam(teamId);
        _stageClientMock
            .Setup(c => c.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StageInfo> { new(stage1Id, 1), new(Guid.NewGuid(), 2) });
        _stageClientMock
            .Setup(c => c.GetStageWithOptionsAsync(stage1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(
                stage1Id, "Etapa 1", "Trivia", 1, 100, "¿?", new List<TriviaOptionInfo>()));

        await _autoStart.Handle(new AutoStartTeamCommand(sessionId, teamId), default);
        var queryResult = await _facade.GetCurrentStageAsync(sessionId, teamId, default);

        queryResult.IsSuccess.Should().BeTrue();
        queryResult.Value.CurrentStageOrder.Should().Be(1);
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never); // no re-avanza
    }
}
