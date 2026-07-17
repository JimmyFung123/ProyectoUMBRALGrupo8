namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using Moq;
using SessionService.Application.Sessions;
using SessionService.Application.Sessions.Facade;
using SessionService.Domain.Sessions;
using Xunit;

public class ParticipantStageFacadeTests
{
    private readonly Mock<ISessionRepository> _sessionRepoMock = new();
    private readonly Mock<ITeamServiceClient> _teamClientMock = new();
    private readonly Mock<IStageServiceClient> _stageClientMock = new();
    private readonly ParticipantStageFacade _facade;

    public ParticipantStageFacadeTests()
    {
        _facade = new ParticipantStageFacade(
            _sessionRepoMock.Object,
            _teamClientMock.Object,
            _stageClientMock.Object);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Session SetupSession(bool inProgress = false)
    {
        var session = Session.Create(Guid.NewGuid(), "Sesión de prueba").Value;
        if (inProgress) session.Start();
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        return session;
    }

    private void SetupTeam(Guid teamId, int currentStageOrder)
    {
        _teamClientMock
            .Setup(c => c.GetTeamByIdAsync(teamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TeamInfoItem(teamId, "Equipo Alfa", currentStageOrder));
    }

    private void SetupStages(params StageInfo[] stages) =>
        _stageClientMock
            .Setup(c => c.GetStagesByMissionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stages.ToList());

    // ── Rutas de fallo ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentStage_WhenSessionNotFound_ReturnsNotFound()
    {
        _sessionRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    [Fact]
    public async Task GetCurrentStage_WhenTeamNotFound_ReturnsTeamNotFound()
    {
        SetupSession();
        _teamClientMock
            .Setup(c => c.GetTeamByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TeamInfoItem?)null);

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), Guid.NewGuid(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.TeamNotFound);
    }

    [Fact]
    public async Task GetCurrentStage_WhenMissionHasNoStages_ReturnsNotFound()
    {
        var teamId = Guid.NewGuid();
        SetupSession();
        SetupTeam(teamId, currentStageOrder: 1);
        SetupStages(); // empty

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    [Fact]
    public async Task GetCurrentStage_WhenStageOrderHasNoMatchingStage_ReturnsNotFound()
    {
        var teamId = Guid.NewGuid();
        SetupSession();
        SetupTeam(teamId, currentStageOrder: 2);           // order 2 requested...
        SetupStages(new StageInfo(Guid.NewGuid(), 1), new StageInfo(Guid.NewGuid(), 3)); // ...but only 1 and 3 exist (maxOrder 3)

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
        _stageClientMock.Verify(
            c => c.GetStageWithOptionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCurrentStage_WhenStageDetailMissing_ReturnsNotFound()
    {
        var teamId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        SetupSession();
        SetupTeam(teamId, currentStageOrder: 1);
        SetupStages(new StageInfo(stageId, 1));
        _stageClientMock
            .Setup(c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageWithOptionsInfo?)null);

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SessionErrors.NotFound);
    }

    // ── Estados centinela (Waiting / Completed) ──────────────────────────────────

    [Fact]
    public async Task GetCurrentStage_WhenTeamNotStartedAndSessionPending_ReturnsWaiting()
    {
        var teamId = Guid.NewGuid();
        SetupSession();                                    // Pending
        SetupTeam(teamId, currentStageOrder: 0);
        SetupStages(new StageInfo(Guid.NewGuid(), 1));

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Waiting");
        result.Value.Type.Should().Be("Waiting");
        result.Value.CurrentStageOrder.Should().Be(0);
        result.Value.IsLastStage.Should().BeFalse();
        result.Value.SessionStatus.Should().Be("Pending");
        // No se intenta auto-arrancar mientras la sesión sigue Pending.
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCurrentStage_WhenTeamFinishedAllStages_ReturnsCompleted()
    {
        var teamId = Guid.NewGuid();
        SetupSession();
        SetupTeam(teamId, currentStageOrder: 5);           // beyond maxOrder
        SetupStages(new StageInfo(Guid.NewGuid(), 1), new StageInfo(Guid.NewGuid(), 2));

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Completed");
        result.Value.CurrentStageOrder.Should().Be(5);
        result.Value.IsLastStage.Should().BeTrue();
    }

    // ── Lectura pura: la fachada ya NO auto-arranca ──────────────────────────────
    // El auto-arranque (escritura) se movió a AutoStartTeamCommand. Aun con la sesión
    // en curso y el equipo en orden 0, la fachada solo refleja el estado actual
    // (Waiting) y NUNCA escribe en TeamService.

    [Fact]
    public async Task GetCurrentStage_WhenSessionInProgressAndTeamAtZero_ReturnsWaiting_AndDoesNotWrite()
    {
        var teamId = Guid.NewGuid();
        SetupSession(inProgress: true);
        SetupTeam(teamId, currentStageOrder: 0);
        SetupStages(new StageInfo(Guid.NewGuid(), 1), new StageInfo(Guid.NewGuid(), 2));

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Waiting");
        result.Value.CurrentStageOrder.Should().Be(0);
        // La fachada es lectura pura: no avanza al equipo ni carga el detalle de etapa.
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _stageClientMock.Verify(
            c => c.GetStageWithOptionsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCurrentStage_WhenTeamAlreadyAtStageOne_ReturnsThatStage()
    {
        // Tras el auto-arranque (hecho por el comando), el equipo llega aquí en orden 1
        // y la fachada simplemente refleja esa etapa.
        var teamId = Guid.NewGuid();
        var stage1Id = Guid.NewGuid();
        SetupSession(inProgress: true);
        SetupTeam(teamId, currentStageOrder: 1);
        SetupStages(new StageInfo(stage1Id, 1), new StageInfo(Guid.NewGuid(), 2));
        _stageClientMock
            .Setup(c => c.GetStageWithOptionsAsync(stage1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(
                stage1Id, "Etapa 1", "Trivia", 1, 100, "¿?", new List<TriviaOptionInfo>()));

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.StageId.Should().Be(stage1Id);
        result.Value.CurrentStageOrder.Should().Be(1);
        _teamClientMock.Verify(
            c => c.ForceAdvanceTeamAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── Camino feliz ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentStage_WhenTriviaStage_MapsDtoHidesIsCorrectAndOmitsCoordinates()
    {
        var teamId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var correctOptionId = Guid.NewGuid();
        var wrongOptionId = Guid.NewGuid();

        SetupSession();
        SetupTeam(teamId, currentStageOrder: 1);
        SetupStages(new StageInfo(stageId, 1), new StageInfo(Guid.NewGuid(), 2));
        _stageClientMock
            .Setup(c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(
                stageId, "Etapa 1", "Trivia", 1, 100, "¿Capital de Panamá?",
                new List<TriviaOptionInfo>
                {
                    new(correctOptionId, "Ciudad de Panamá", IsCorrect: true),
                    new(wrongOptionId, "Colón", IsCorrect: false),
                },
                Latitude: 8.98, Longitude: -79.51)); // coords presentes pero NO es TreasureHunt

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.StageId.Should().Be(stageId);
        dto.Title.Should().Be("Etapa 1");
        dto.Question.Should().Be("¿Capital de Panamá?");
        dto.CurrentStageOrder.Should().Be(1);
        dto.IsLastStage.Should().BeFalse(); // 1 de 2

        // Las coordenadas solo se exponen en TreasureHunt.
        dto.Latitude.Should().BeNull();
        dto.Longitude.Should().BeNull();

        // El DTO de participante no debe exponer cuál opción es la correcta.
        dto.Options.Should().HaveCount(2);
        dto.Options.Select(o => o.Id).Should().BeEquivalentTo(new[] { correctOptionId, wrongOptionId });
        dto.Options.Should().AllBeOfType<SessionService.Application.Sessions.Queries.GetParticipantStage.ParticipantOptionDto>();
    }

    [Fact]
    public async Task GetCurrentStage_WhenTreasureHuntLastStage_ExposesCoordinatesAndIsLast()
    {
        var teamId = Guid.NewGuid();
        var stageId = Guid.NewGuid();

        SetupSession();
        SetupTeam(teamId, currentStageOrder: 2);
        SetupStages(new StageInfo(Guid.NewGuid(), 1), new StageInfo(stageId, 2));
        _stageClientMock
            .Setup(c => c.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(
                stageId, "El Tesoro", "TreasureHunt", 2, 250, null,
                new List<TriviaOptionInfo>(),
                Latitude: 10.0, Longitude: -66.0, QrCode: "SECRET-QR"));

        var result = await _facade.GetCurrentStageAsync(Guid.NewGuid(), teamId, default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.Type.Should().Be("TreasureHunt");
        dto.IsLastStage.Should().BeTrue(); // 2 de 2
        dto.Latitude.Should().Be(10.0);
        dto.Longitude.Should().Be(-66.0);
        dto.Options.Should().BeEmpty();
    }
}
