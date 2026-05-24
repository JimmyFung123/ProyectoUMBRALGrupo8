namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Teams.Commands.CreateTeam;
using TeamService.Domain.Teams;
using Xunit;

public class CreateTeamCommandHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly CreateTeamCommandHandler _handler;

    public CreateTeamCommandHandlerTests()
        => _handler = new CreateTeamCommandHandler(_repoMock.Object);

    [Fact]
    public async Task Handle_EmptyTeamName_ReturnsValidationError()
    {
        var result = await _handler.Handle(new CreateTeamCommand(Guid.NewGuid(), "  "), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.InvalidTeamName);
        _repoMock.Verify(r => r.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesTeamWithInviteCode()
    {
        Team? saved = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
                 .Callback<Team, CancellationToken>((t, _) => saved = t)
                 .Returns(Task.CompletedTask);

        var result = await _handler.Handle(new CreateTeamCommand(Guid.NewGuid(), "Los Campeones"), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.TeamId.Should().NotBe(Guid.Empty);
        result.Value.InviteCode.Should().HaveLength(4);
        saved.Should().NotBeNull();
        saved!.Name.Should().Be("Los Campeones");
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
