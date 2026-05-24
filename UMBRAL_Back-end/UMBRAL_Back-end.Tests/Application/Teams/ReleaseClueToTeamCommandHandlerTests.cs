namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Commands.ReleaseClue;
using TeamService.Domain.Teams;
using Xunit;

public class ReleaseClueToTeamCommandHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly ReleaseClueCommandHandler _handler;

    public ReleaseClueToTeamCommandHandlerTests()
        => _handler = new ReleaseClueCommandHandler(_repoMock.Object);

    // ── Team not found ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamNotFound_ReturnsNotFoundError()
    {
        var teamId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Team?)null);

        var result = await _handler.Handle(new ReleaseClueCommand(teamId, 3), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.NotFound);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── All clues already released ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenAllCluesReleased_ReturnsExhaustedError()
    {
        var teamId = Guid.NewGuid();
        var team = Team.Create(Guid.NewGuid(), "Equipo Alfa");

        // Simulate team already having received all 2 clues
        team.ReceiveClue(2); // cluesReceived = 1
        team.ReceiveClue(2); // cluesReceived = 2

        _repoMock.Setup(r => r.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new ReleaseClueCommand(teamId, 2), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.AllCluesReleased);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Happy path: first clue ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenCluesAvailable_IncrementsAndSaves()
    {
        var teamId = Guid.NewGuid();
        var team = Team.Create(Guid.NewGuid(), "Equipo Beta");

        _repoMock.Setup(r => r.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new ReleaseClueCommand(teamId, 3), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1); // cluesReceivedCurrentStage is now 1
        team.CluesReceivedCurrentStage.Should().Be(1);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Happy path: sequential releases ──────────────────────────────────────

    [Fact]
    public async Task Handle_MultipleReleases_IncrementsSequentially()
    {
        var teamId = Guid.NewGuid();
        var team = Team.Create(Guid.NewGuid(), "Equipo Gamma");

        _repoMock.Setup(r => r.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        // Release clue 1
        var r1 = await _handler.Handle(new ReleaseClueCommand(teamId, 3), default);
        // Release clue 2
        var r2 = await _handler.Handle(new ReleaseClueCommand(teamId, 3), default);
        // Release clue 3 (last)
        var r3 = await _handler.Handle(new ReleaseClueCommand(teamId, 3), default);

        r1.Value.Should().Be(1);
        r2.Value.Should().Be(2);
        r3.Value.Should().Be(3);
        team.CluesReceivedCurrentStage.Should().Be(3);
    }

    // ── One clue remaining ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenOneClueRemaining_ReleasesLastOne()
    {
        var teamId = Guid.NewGuid();
        var team = Team.Create(Guid.NewGuid(), "Equipo Delta");
        team.ReceiveClue(2); // already has 1 of 2

        _repoMock.Setup(r => r.GetByIdAsync(teamId, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(new ReleaseClueCommand(teamId, 2), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        // Now exhausted
        var exhausted = await _handler.Handle(new ReleaseClueCommand(teamId, 2), default);
        exhausted.IsFailure.Should().BeTrue();
        exhausted.Error.Should().Be(TeamErrors.AllCluesReleased);
    }
}
