namespace UMBRAL_Back_end.Tests.Application.Missions;

using FluentAssertions;
using Moq;
using SessionService.Application.Missions.Composite;
using SessionService.Application.Missions.Queries.GetMissionStructure;
using SessionService.Application.Sessions;
using SessionService.Domain.MissionLookup;
using Xunit;

/// <summary>
/// Pruebas del builder del arbol Composite (extraido del handler por SRP). Verifican
/// el ensamblado en aislamiento: orden de etapas/pistas, fallback de detalle nulo y
/// la estructura resultante Mission -> Stages -> Clues.
/// </summary>
public class MissionStructureTreeBuilderTests
{
    private readonly Mock<IStageServiceClient> _stageClientMock = new();
    private readonly Mock<IClueServiceClient> _clueClientMock = new();
    private readonly MissionStructureTreeBuilder _builder;

    public MissionStructureTreeBuilderTests()
    {
        _builder = new MissionStructureTreeBuilder(_stageClientMock.Object, _clueClientMock.Object);
    }

    [Fact]
    public async Task BuildAsync_AssemblesTree_OrdersStagesAndClues()
    {
        var missionId = Guid.NewGuid();
        var stage1Id = Guid.NewGuid();
        var stage2Id = Guid.NewGuid();
        var lookup = MissionLookup.Create(missionId, "Misión Demo", "Active", "Hard");

        // Etapas devueltas fuera de orden a proposito.
        _stageClientMock
            .Setup(s => s.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stage2Id, 2, "TreasureHunt"), new StageInfo(stage1Id, 1, "Trivia")]);
        _stageClientMock
            .Setup(s => s.GetStageWithOptionsAsync(stage1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(stage1Id, "Pregunta 1", "Trivia", 1, 100, "¿2+2?", []));
        _stageClientMock
            .Setup(s => s.GetStageWithOptionsAsync(stage2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(stage2Id, "Tesoro", "TreasureHunt", 2, 250, null, []));

        // Pistas de la etapa 1, tambien fuera de orden; la etapa 2 sin pistas.
        _clueClientMock
            .Setup(c => c.GetCluesByStageAsync(stage1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ClueInfo(Guid.NewGuid(), 2, "Pista 2", null, null, null, 5),
                new ClueInfo(Guid.NewGuid(), 1, "Pista 1", null, null, null, 5),
            ]);
        _clueClientMock
            .Setup(c => c.GetCluesByStageAsync(stage2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var mission = await _builder.BuildAsync(lookup, default);

        mission.Id.Should().Be(missionId);
        mission.ComponentType.Should().Be("Mission");

        var stages = mission.Children.OfType<StageComponent>().ToList();
        stages.Should().HaveCount(2);
        stages[0].Order.Should().Be(1);          // ordenadas por Order
        stages[0].Name.Should().Be("Pregunta 1");
        stages[1].Order.Should().Be(2);

        var stage1Clues = stages[0].Children.OfType<ClueComponent>().ToList();
        stage1Clues.Should().HaveCount(2);
        stage1Clues[0].Order.Should().Be(1);     // pistas ordenadas
        stage1Clues[1].Order.Should().Be(2);
        stages[1].Children.Should().BeEmpty();

        // El TotalScore agrega los puntajes base de las etapas (Composite).
        mission.TotalScore().Should().Be(350);   // 100 + 250
    }

    [Fact]
    public async Task BuildAsync_WhenStageDetailMissing_UsesFallbackStageWithZeroScore()
    {
        var missionId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var lookup = MissionLookup.Create(missionId, "Misión", "Active", "Easy");

        _stageClientMock
            .Setup(s => s.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stageId, 3, "Trivia")]);
        // Detalle nulo (p. ej. StageService caido para esa etapa).
        _stageClientMock
            .Setup(s => s.GetStageWithOptionsAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StageWithOptionsInfo?)null);
        _clueClientMock
            .Setup(c => c.GetCluesByStageAsync(stageId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var mission = await _builder.BuildAsync(lookup, default);

        var stage = mission.Children.OfType<StageComponent>().Single();
        stage.Name.Should().Be("Etapa 3");   // fallback por orden
        stage.Order.Should().Be(3);
        stage.BaseScore.Should().Be(0);       // sin detalle, no aporta puntaje
        mission.TotalScore().Should().Be(0);
    }

    [Fact]
    public async Task BuildAsync_WhenNoStages_ReturnsRootOnly()
    {
        var missionId = Guid.NewGuid();
        var lookup = MissionLookup.Create(missionId, "Vacía", "Inactive", "Easy");
        _stageClientMock
            .Setup(s => s.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var mission = await _builder.BuildAsync(lookup, default);

        mission.Children.Should().BeEmpty();
        mission.TotalScore().Should().Be(0);
    }
}
