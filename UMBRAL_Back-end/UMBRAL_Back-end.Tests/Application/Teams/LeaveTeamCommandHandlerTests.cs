namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Rankings;
using TeamService.Application.Teams.Commands.LeaveTeam;
using TeamService.Domain.Teams;
using Xunit;

public class LeaveTeamCommandHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly Mock<IRankingProjector> _projectorMock = new();
    private readonly LeaveTeamCommandHandler _handler;

    public LeaveTeamCommandHandlerTests()
        => _handler = new LeaveTeamCommandHandler(_repoMock.Object, _projectorMock.Object);

    [Fact]
    public async Task Handle_TeamNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Team?)null);

        var result = await _handler.Handle(new LeaveTeamCommand(Guid.NewGuid()), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.NotFound);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _projectorMock.Verify(p => p.RebuildAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TeamWithMultipleMembers_DecrementsCountAndDoesNotDelete()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Los Genios").Value); // MemberCount = 1
        team.Join(); // MemberCount = 2
        _repoMock.Setup(r => r.GetByIdAsync(team.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new LeaveTeamCommand(team.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
        team.MemberCount.Should().Be(1);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _projectorMock.Verify(p => p.RebuildAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LastMemberLeaves_DeletesTeamAndRebuildsRanking()
    {
        var sessionId = Guid.NewGuid();
        var team = Team.Create(sessionId, TeamName.Create("Los Solitarios").Value); // MemberCount = 1
        _repoMock.Setup(r => r.GetByIdAsync(team.Id, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new LeaveTeamCommand(team.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
        team.MemberCount.Should().Be(0);
        _repoMock.Verify(r => r.DeleteAsync(team.Id, It.IsAny<CancellationToken>()), Times.Once);
        _projectorMock.Verify(p => p.RebuildAsync(sessionId, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
