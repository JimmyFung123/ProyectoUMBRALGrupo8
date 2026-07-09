namespace UMBRAL_Back_end.Tests.Application.Stages;

using FluentAssertions;
using Moq;
using StageService.Application.Stages.Queries.GetStagesByMission;
using StageService.Domain.Stages;
using Xunit;

public class GetStagesByMissionQueryHandlerTests
{
    private readonly Mock<IStageRepository> _repoMock = new();
    private readonly GetStagesByMissionQueryHandler _handler;

    public GetStagesByMissionQueryHandlerTests()
        => _handler = new GetStagesByMissionQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenNoStages_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetByMissionIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Stage>());

        var result = await _handler.Handle(new GetStagesByMissionQuery(Guid.NewGuid()), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MapsStagesToDto()
    {
        var missionId = Guid.NewGuid();
        var stages = new List<Stage>
        {
            Stage.Create(missionId, "Etapa 1", StageType.Trivia, 1, 100, "¿Pregunta?").Value,
        };
        _repoMock.Setup(r => r.GetByMissionIdAsync(missionId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(stages);

        var result = await _handler.Handle(new GetStagesByMissionQuery(missionId), default);

        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Etapa 1");
        result[0].Type.Should().Be("Trivia");
        result[0].Order.Should().Be(1);
        result[0].BaseScore.Should().Be(100);
    }
}
