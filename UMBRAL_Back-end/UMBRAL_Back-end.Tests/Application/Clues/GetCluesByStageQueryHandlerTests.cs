namespace UMBRAL_Back_end.Tests.Application.Clues;

using FluentAssertions;
using Moq;
using ClueService.Application.Clues.Queries.GetCluesByStage;
using ClueService.Domain.Clues;
using Xunit;

public class GetCluesByStageQueryHandlerTests
{
    private readonly Mock<IClueRepository> _repoMock = new();
    private readonly GetCluesByStageQueryHandler _handler;

    public GetCluesByStageQueryHandlerTests()
        => _handler = new GetCluesByStageQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenNoClues_ReturnsEmptyList()
    {
        _repoMock.Setup(r => r.GetByStageIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Clue>());

        var result = await _handler.Handle(new GetCluesByStageQuery(Guid.NewGuid()), default);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsCluesOrderedByOrderAndMapped()
    {
        var stageId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var clues = new List<Clue>
        {
            Clue.Create(stageId, missionId, "Trivia", 2, "Segunda", null, null, null).Value,
            Clue.Create(stageId, missionId, "Trivia", 1, "Primera", null, null, null).Value,
        };
        _repoMock.Setup(r => r.GetByStageIdAsync(stageId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(clues);

        var result = await _handler.Handle(new GetCluesByStageQuery(stageId), default);

        result.Should().HaveCount(2);
        result[0].Order.Should().Be(1);
        result[0].Content.Should().Be("Primera");
        result[1].Order.Should().Be(2);
    }
}
