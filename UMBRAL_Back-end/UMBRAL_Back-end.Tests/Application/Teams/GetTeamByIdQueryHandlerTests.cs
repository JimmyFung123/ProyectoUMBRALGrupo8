namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Queries.GetTeamById;
using TeamService.Domain.Teams;
using Xunit;

public class GetTeamByIdQueryHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly GetTeamByIdQueryHandler _handler;

    public GetTeamByIdQueryHandlerTests()
        => _handler = new GetTeamByIdQueryHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenTeamNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Team?)null);

        var result = await _handler.Handle(new GetTeamByIdQuery(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.NotFound);
    }

    [Fact]
    public async Task Handle_WhenTeamFound_MapsFieldsToDto()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Los Halcones").Value);
        _repoMock.Setup(r => r.GetByIdAsync(team.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new GetTeamByIdQuery(team.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TeamId.Should().Be(team.Id);
        result.Value.TeamName.Should().Be("Los Halcones");
        result.Value.InviteCode.Should().Be(team.InviteCode);
        result.Value.MemberCount.Should().Be(team.MemberCount);
        result.Value.CurrentStageOrder.Should().Be(team.CurrentStageOrder);
    }
}
