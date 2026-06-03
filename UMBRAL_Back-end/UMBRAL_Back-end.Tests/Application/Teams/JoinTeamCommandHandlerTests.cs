namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Commands.JoinTeam;
using TeamService.Domain.Teams;
using Xunit;

public class JoinTeamCommandHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly JoinTeamCommandHandler _handler;

    public JoinTeamCommandHandlerTests()
        => _handler = new JoinTeamCommandHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenInviteCodeNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByInviteCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Team?)null);

        var result = await _handler.Handle(new JoinTeamCommand("XXXX"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.TeamNotFound);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCode_JoinsAndIncreasesMemberCount()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Los Genios").Value); // MemberCount = 1
        _repoMock.Setup(r => r.GetByInviteCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new JoinTeamCommand(team.InviteCode), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.MemberCount.Should().Be(2);
        result.Value.TeamName.Should().Be("Los Genios");
        result.Value.InviteCode.Should().Be(team.InviteCode);
        team.MemberCount.Should().Be(2);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SecondJoin_MemberCountReachesThree()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Los Tres").Value);
        team.Join(); // MemberCount = 2
        _repoMock.Setup(r => r.GetByInviteCodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new JoinTeamCommand(team.InviteCode), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.MemberCount.Should().Be(3);
    }
}
