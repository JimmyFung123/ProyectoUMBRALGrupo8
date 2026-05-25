namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

public class TeamTriviaAnswerTests
{
    [Fact]
    public void AnswerTrivia_CorrectAnswer_IncreasesScoreByScoreChange()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        team.UpdateScore(100);

        var result = team.AnswerTrivia(isCorrect: true, scoreChange: 50, nextStageOrder: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(150);
        team.Score.Should().Be(150);
    }

    [Fact]
    public void AnswerTrivia_IncorrectAnswer_DecreasesScoreByScoreChange()
    {
        var team = Team.Create(Guid.NewGuid(), "Beta");
        team.UpdateScore(100);

        var result = team.AnswerTrivia(isCorrect: false, scoreChange: 50, nextStageOrder: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(50);
        team.Score.Should().Be(50);
    }

    [Fact]
    public void AnswerTrivia_IncorrectAnswer_ScoreCanGoNegative()
    {
        var team = Team.Create(Guid.NewGuid(), "Gamma");
        team.UpdateScore(10);

        var result = team.AnswerTrivia(isCorrect: false, scoreChange: 50, nextStageOrder: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(-40);
        team.Score.Should().Be(-40);
    }

    [Fact]
    public void AnswerTrivia_AdvancesCurrentStageOrderToNextStageOrder()
    {
        var team = Team.Create(Guid.NewGuid(), "Delta");

        team.AnswerTrivia(isCorrect: true, scoreChange: 30, nextStageOrder: 3);

        team.CurrentStageOrder.Should().Be(3);
    }

    [Fact]
    public void AnswerTrivia_ResetsCluesReceivedCurrentStageToZero()
    {
        var team = Team.Create(Guid.NewGuid(), "Epsilon");
        // Simulate clues received by using UpdateProgress
        team.UpdateProgress(stageOrder: 1, cluesCurrentStage: 2, totalClues: 5);

        team.AnswerTrivia(isCorrect: true, scoreChange: 30, nextStageOrder: 2);

        team.CluesReceivedCurrentStage.Should().Be(0);
    }

    [Fact]
    public void AnswerTrivia_SetsClueTimerResetAtToNonNull()
    {
        var team = Team.Create(Guid.NewGuid(), "Zeta");

        team.AnswerTrivia(isCorrect: true, scoreChange: 30, nextStageOrder: 2);

        team.ClueTimerResetAt.Should().NotBeNull();
    }

    // ── HU-21: tiempo de resolución (LastStageCompletedAt) ────────────────────

    [Fact]
    public void AnswerTrivia_CorrectAnswer_StampsLastStageCompletedAt()
    {
        var team = Team.Create(Guid.NewGuid(), "Resolutor");
        team.LastStageCompletedAt.Should().BeNull();

        var before = DateTime.UtcNow;
        team.AnswerTrivia(isCorrect: true, scoreChange: 50, nextStageOrder: 2);
        var after = DateTime.UtcNow;

        team.LastStageCompletedAt.Should().NotBeNull();
        team.LastStageCompletedAt!.Value.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void AnswerTrivia_IncorrectAnswer_DoesNotStampLastStageCompletedAt()
    {
        var team = Team.Create(Guid.NewGuid(), "Errado");

        team.AnswerTrivia(isCorrect: false, scoreChange: 50, nextStageOrder: 2);

        team.LastStageCompletedAt.Should().BeNull();
    }
}
