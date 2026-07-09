namespace UMBRAL_Back_end.Tests.Application.Missions;

using FluentAssertions;
using Moq;
using UMBRAL_Back_end.Application.Missions.Queries.GetMissionById;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class GetMissionByIdQueryHandlerTests
{
    private readonly Mock<IMissionRepository> _repoMock = new();
    private readonly GetMissionByIdQueryHandler _handler;

    public GetMissionByIdQueryHandlerTests()
        => _handler = new GetMissionByIdQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenMissionNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Mission?)null);

        var result = await _handler.Handle(new GetMissionByIdQuery(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MissionErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenMissionFound_MapsAllFieldsToDto()
    {
        var mission = Mission.Create("Alpha Protocol", "Una misión", DifficultyLevel.Hard, 90).Value;
        _repoMock.Setup(r => r.GetByIdAsync(mission.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(mission);

        var result = await _handler.Handle(new GetMissionByIdQuery(mission.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(mission.Id);
        result.Value.Name.Should().Be("Alpha Protocol");
        result.Value.Description.Should().Be("Una misión");
        result.Value.Difficulty.Should().Be("Hard");
        result.Value.MaxDuration.Should().Be(90);
        result.Value.Status.Should().Be(mission.Status.ToString());
    }
}
