namespace UMBRAL_Back_end.Tests.Application.Missions;

using FluentAssertions;
using Moq;
using UMBRAL_Back_end.Application.Missions.Queries.GetMissions;
using UMBRAL_Back_end.Domain.Missions;
using Xunit;

public class GetMissionsQueryHandlerTests
{
    private readonly Mock<IMissionRepository> _repoMock = new();
    private readonly GetMissionsQueryHandler _handler;

    public GetMissionsQueryHandlerTests()
        => _handler = new GetMissionsQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_MapsMissionsToDtoList()
    {
        var missions = new List<Mission>
        {
            Mission.Create("Alpha", "d1", DifficultyLevel.Easy, 30).Value,
            Mission.Create("Beta", "d2", DifficultyLevel.Medium, 60).Value,
        };
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<MissionStatus?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(missions);

        var result = await _handler.Handle(new GetMissionsQuery(), default);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Alpha");
        result[1].Difficulty.Should().Be("Medium");
    }

    [Fact]
    public async Task Handle_WhenStatusIsValid_ForwardsParsedStatusToRepository()
    {
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<MissionStatus?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Mission>());

        await _handler.Handle(new GetMissionsQuery("Active"), default);

        _repoMock.Verify(r => r.GetAllAsync(MissionStatus.Active, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenStatusIsInvalid_PassesNullStatus()
    {
        _repoMock.Setup(r => r.GetAllAsync(It.IsAny<MissionStatus?>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new List<Mission>());

        await _handler.Handle(new GetMissionsQuery("NoExiste"), default);

        _repoMock.Verify(r => r.GetAllAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
