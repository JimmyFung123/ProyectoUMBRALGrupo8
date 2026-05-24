namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Commands.ForceAdvance;
using TeamService.Domain.Teams;
using Xunit;

public class ForceAdvanceTeamCommandHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly ForceAdvanceTeamCommandHandler _handler;

    public ForceAdvanceTeamCommandHandlerTests()
        => _handler = new ForceAdvanceTeamCommandHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenTeamNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Team?)null);

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(Guid.NewGuid(), 2), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.NotFound);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenNextStageSameAsCurrent_ReturnsInvalidNextStageError()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        team.UpdateProgress(2, 0, 3);
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(team.Id, 2), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.InvalidNextStage);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidAdvance_AdvancesAndSaves()
    {
        var team = Team.Create(Guid.NewGuid(), "Beta");
        team.UpdateProgress(1, 2, 3);
        team.UpdateScore(100);
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new ForceAdvanceTeamCommand(team.Id, 2), default);

        result.IsSuccess.Should().BeTrue();
        team.CurrentStageOrder.Should().Be(2);
        team.CluesReceivedCurrentStage.Should().Be(0);
        team.Score.Should().Be(100); // score unchanged
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
