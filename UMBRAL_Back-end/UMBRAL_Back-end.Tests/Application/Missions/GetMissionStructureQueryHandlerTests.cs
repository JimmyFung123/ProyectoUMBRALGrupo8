namespace UMBRAL_Back_end.Tests.Application.Missions;

using FluentAssertions;
using Moq;
using SessionService.Application.Missions.Queries.GetMissionStructure;
using SessionService.Application.Sessions;
using SessionService.Domain.MissionLookup;
using Xunit;

public class GetMissionStructureQueryHandlerTests
{
    private readonly Mock<IMissionLookupRepository> _missionLookupMock = new();
    private readonly Mock<IStageServiceClient> _stageClientMock = new();
    private readonly Mock<IClueServiceClient> _clueClientMock = new();
    private readonly GetMissionStructureQueryHandler _handler;

    public GetMissionStructureQueryHandlerTests()
    {
        _handler = new GetMissionStructureQueryHandler(
            _missionLookupMock.Object,
            _stageClientMock.Object,
            _clueClientMock.Object);
    }

    [Fact]
    public async Task Handle_WhenMissionUnknown_ReturnsNotFound()
    {
        var missionId = Guid.NewGuid();
        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MissionLookup?)null);

        var result = await _handler.Handle(new GetMissionStructureQuery(missionId), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(MissionStructureErrors.MissionNotFound.Code);
    }

    [Fact]
    public async Task Handle_AssemblesFullTree_WithStagesCluesAndSummary()
    {
        var missionId = Guid.NewGuid();
        var stage1Id = Guid.NewGuid();
        var stage2Id = Guid.NewGuid();

        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MissionLookup.Create(missionId, "Misión Demo", "Active", "Hard"));

        // Two stages, returned out of order to prove the handler sorts by Order.
        _stageClientMock
            .Setup(s => s.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new StageInfo(stage2Id, 2, "TreasureHunt"), new StageInfo(stage1Id, 1, "Trivia")]);

        _stageClientMock
            .Setup(s => s.GetStageWithOptionsAsync(stage1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(stage1Id, "Pregunta 1", "Trivia", 1, 100, "¿2+2?", []));

        _stageClientMock
            .Setup(s => s.GetStageWithOptionsAsync(stage2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StageWithOptionsInfo(stage2Id, "Tesoro", "TreasureHunt", 2, 250, null, [], 10.0, -66.0, "QR-1"));

        // Stage 1 has two clues (out of order); stage 2 has none.
        _clueClientMock
            .Setup(c => c.GetCluesByStageAsync(stage1Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ClueInfo(Guid.NewGuid(), 2, "Pista 2", null, null, null, 5),
                new ClueInfo(Guid.NewGuid(), 1, "Pista 1", null, null, null, 5),
            ]);
        _clueClientMock
            .Setup(c => c.GetCluesByStageAsync(stage2Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new GetMissionStructureQuery(missionId), default);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;

        dto.MissionId.Should().Be(missionId);
        dto.Name.Should().Be("Misión Demo");
        dto.Difficulty.Should().Be("Hard");
        dto.Status.Should().Be("Active");

        // Stages ordered by Order.
        dto.Stages.Should().HaveCount(2);
        dto.Stages[0].Order.Should().Be(1);
        dto.Stages[0].Title.Should().Be("Pregunta 1");
        dto.Stages[1].Order.Should().Be(2);

        // Clues ordered within the stage.
        dto.Stages[0].Clues.Should().HaveCount(2);
        dto.Stages[0].Clues[0].Order.Should().Be(1);
        dto.Stages[0].Clues[1].Order.Should().Be(2);
        dto.Stages[1].Clues.Should().BeEmpty();

        // Aggregated summary.
        dto.Summary.StageCount.Should().Be(2);
        dto.Summary.ClueCount.Should().Be(2);
        dto.Summary.TriviaStages.Should().Be(1);
        dto.Summary.TreasureHuntStages.Should().Be(1);
        dto.Summary.TotalScore.Should().Be(350); // 100 + 250
        dto.Summary.StagesWithoutClues.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenMissionHasNoStages_ReturnsEmptyTreeWithZeroSummary()
    {
        var missionId = Guid.NewGuid();
        _missionLookupMock
            .Setup(r => r.GetByIdAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MissionLookup.Create(missionId, "Vacía", "Inactive", "Easy"));
        _stageClientMock
            .Setup(s => s.GetStagesByMissionAsync(missionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _handler.Handle(new GetMissionStructureQuery(missionId), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Stages.Should().BeEmpty();
        result.Value.Summary.StageCount.Should().Be(0);
        result.Value.Summary.TotalScore.Should().Be(0);
    }
}
