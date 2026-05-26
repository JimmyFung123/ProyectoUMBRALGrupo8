namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Rankings;
using TeamService.Application.Teams.Commands.AnswerTrivia;
using TeamService.Domain.Teams;
using Xunit;

public class AnswerTriviaCommandHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly Mock<IRankingProjector> _projectorMock = new();
    private readonly AnswerTriviaCommandHandler _handler;

    public AnswerTriviaCommandHandlerTests()
        => _handler = new AnswerTriviaCommandHandler(_repoMock.Object, _projectorMock.Object);

    [Fact]
    public async Task Handle_WhenTeamNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Team?)null);

        var result = await _handler.Handle(
            new AnswerTriviaCommand(Guid.NewGuid(), true, 50, 2), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.NotFound);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _projectorMock.Verify(p => p.RebuildAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_CorrectAnswer_SavesAndReturnsIncreasedScore()
    {
        var sessionId = Guid.NewGuid();
        var team = Team.Create(sessionId, "Alpha");
        team.UpdateScore(100);
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(
            new AnswerTriviaCommand(team.Id, IsCorrect: true, ScoreChange: 50, NextStageOrder: 2), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(150);
        team.Score.Should().Be(150);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        // HU-24: Score + LastStageCompletedAt changed — projection must be rebuilt.
        _projectorMock.Verify(
            p => p.RebuildAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_IncorrectAnswer_SavesAndReturnsDecreasedScore()
    {
        var sessionId = Guid.NewGuid();
        var team = Team.Create(sessionId, "Beta");
        team.UpdateScore(100);
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(
            new AnswerTriviaCommand(team.Id, IsCorrect: false, ScoreChange: 50, NextStageOrder: 2), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(50);
        team.Score.Should().Be(50);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _projectorMock.Verify(
            p => p.RebuildAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
