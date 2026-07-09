namespace UMBRAL_Back_end.Tests.Application.Stages;

using FluentAssertions;
using Moq;
using StageService.Application.Stages.Queries.GetStageById;
using StageService.Domain.Stages;
using Xunit;

public class GetStageByIdQueryHandlerTests
{
    private readonly Mock<IStageRepository> _repoMock = new();
    private readonly GetStageByIdQueryHandler _handler;

    public GetStageByIdQueryHandlerTests()
        => _handler = new GetStageByIdQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenStageNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Stage?)null);

        var result = await _handler.Handle(new GetStageByIdQuery(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(StageErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenStageFound_MapsFieldsToDto()
    {
        var missionId = Guid.NewGuid();
        var stage = Stage.Create(missionId, "Etapa trivia", StageType.Trivia, 2, 50, "¿Cuál?").Value;
        _repoMock.Setup(r => r.GetByIdAsync(stage.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(stage);

        var result = await _handler.Handle(new GetStageByIdQuery(stage.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(stage.Id);
        result.Value.MissionId.Should().Be(missionId);
        result.Value.Title.Should().Be("Etapa trivia");
        result.Value.Type.Should().Be("Trivia");
        result.Value.Order.Should().Be(2);
        result.Value.BaseScore.Should().Be(50);
    }
}
