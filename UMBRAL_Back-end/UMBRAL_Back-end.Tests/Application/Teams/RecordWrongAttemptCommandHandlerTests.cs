namespace UMBRAL_Back_end.Tests.Application.Teams;

using FluentAssertions;
using Moq;
using TeamService.Application.Rankings;
using TeamService.Application.Teams.Commands.RecordWrongAttempt;
using TeamService.Domain.Teams;
using Xunit;

public class RecordWrongAttemptCommandHandlerTests
{
    private readonly Mock<ITeamRepository> _repoMock = new();
    private readonly Mock<IRankingProjector> _projectorMock = new();
    private readonly RecordWrongAttemptCommandHandler _handler;

    public RecordWrongAttemptCommandHandlerTests()
        => _handler = new RecordWrongAttemptCommandHandler(_repoMock.Object, _projectorMock.Object);

    private static Team TeamWithScore(Guid sessionId, int score)
    {
        var team = Team.Create(sessionId, TeamName.Create("Alpha").Value);
        team.UpdateScore(score);
        return team;
    }

    // ── Team not found ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTeamNotFound_ReturnsNotFoundError()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync((Team?)null);

        var result = await _handler.Handle(
            new RecordWrongAttemptCommand(Guid.NewGuid(), Guid.NewGuid(), -25), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.NotFound);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _projectorMock.Verify(p => p.RebuildAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── First wrong attempt ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_FirstWrongAttempt_AppliesPenaltyIncrementsCountAndSaves()
    {
        var sessionId = Guid.NewGuid();
        var team = TeamWithScore(sessionId, 100);
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var result = await _handler.Handle(
            new RecordWrongAttemptCommand(team.Id, Guid.NewGuid(), -25), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewWrongCount.Should().Be(1);
        result.Value.NewScore.Should().Be(75);
        team.WrongAttemptsCurrentStage.Should().Be(1);
        team.Score.Should().Be(75);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Blocked option is recorded ────────────────────────────────────────────

    [Fact]
    public async Task Handle_WrongAttempt_AddsChosenOptionToBlockedList()
    {
        var team = TeamWithScore(Guid.NewGuid(), 100);
        var blockedOptionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        await _handler.Handle(
            new RecordWrongAttemptCommand(team.Id, blockedOptionId, -25), default);

        team.BlockedOptionIds.Should().Contain(blockedOptionId.ToString());
    }

    // ── Repeated wrong attempts accumulate ────────────────────────────────────

    [Fact]
    public async Task Handle_MultipleWrongAttempts_AccumulateCountAndStackPenalty()
    {
        var team = TeamWithScore(Guid.NewGuid(), 100);
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        var first = await _handler.Handle(
            new RecordWrongAttemptCommand(team.Id, Guid.NewGuid(), -25), default);
        var second = await _handler.Handle(
            new RecordWrongAttemptCommand(team.Id, Guid.NewGuid(), -25), default);

        first.Value.NewWrongCount.Should().Be(1);
        second.Value.NewWrongCount.Should().Be(2);
        second.Value.NewScore.Should().Be(50);
        team.WrongAttemptsCurrentStage.Should().Be(2);
    }

    // ── Ranking rebuild (HU-24) ───────────────────────────────────────────────

    [Fact]
    public async Task Handle_ValidCommand_RebuildsRankingForTeamSession()
    {
        var sessionId = Guid.NewGuid();
        var team = TeamWithScore(sessionId, 100);
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(team);

        await _handler.Handle(
            new RecordWrongAttemptCommand(team.Id, Guid.NewGuid(), -25), default);

        // A wrong answer lowers the score, so ranks may swap → projection must rebuild.
        _projectorMock.Verify(
            p => p.RebuildAsync(sessionId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
