namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Commands.PenalizeTeam;
using TeamService.Domain.Teams;
using Xunit;

public class PenalizeTeamCommandHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly PenalizeTeamCommandHandler _handler;

    public PenalizeTeamCommandHandlerTests()
        => _handler = new PenalizeTeamCommandHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_WhenTeamNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Team?)null);

        var result = await _handler.Handle(new PenalizeTeamCommand(Guid.NewGuid(), 10, "Cheating"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.NotFound);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenReasonEmpty_ReturnsValidationError()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new PenalizeTeamCommand(team.Id, 10, ""), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.PenaltyReasonRequired);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPointsZero_ReturnsValidationError()
    {
        var team = Team.Create(Guid.NewGuid(), "Beta");
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new PenalizeTeamCommand(team.Id, 0, "Valid reason"), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.InvalidPenaltyPoints);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_PenalizesAndSaves()
    {
        var team = Team.Create(Guid.NewGuid(), "Gamma");
        team.UpdateScore(100);
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new PenalizeTeamCommand(team.Id, 25, "Misconduct"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(75);
        team.Score.Should().Be(75);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
