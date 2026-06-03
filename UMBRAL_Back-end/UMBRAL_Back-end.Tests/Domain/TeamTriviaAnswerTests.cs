namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

public class TeamTriviaAnswerTests
{
    [Fact]
    public void AnswerTrivia_CorrectAnswer_IncreasesScoreByScoreChange()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Alpha").Value);
        team.UpdateScore(100);

        var result = team.AnswerTrivia(isCorrect: true, scoreChange: 50, nextStageOrder: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewScore.Should().Be(150);
        team.Score.Should().Be(150);
    }

    [Fact]
    public void AnswerTrivia_IncorrectAnswer_DecreasesScoreBySignedScoreChange()
    {
        // ScoreChange is now signed: negative = penalty (resolved by IScoringStrategy).
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Beta").Value);
        team.UpdateScore(100);

        var result = team.AnswerTrivia(isCorrect: false, scoreChange: -50, nextStageOrder: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewScore.Should().Be(50);  // 100 + (-50) = 50
        team.Score.Should().Be(50);
    }

    [Fact]
    public void AnswerTrivia_IncorrectAnswer_ScoreCanGoNegative()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Gamma").Value);
        team.UpdateScore(10);

        var result = team.AnswerTrivia(isCorrect: false, scoreChange: -50, nextStageOrder: 2);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewScore.Should().Be(-40);  // 10 + (-50) = -40
        team.Score.Should().Be(-40);
    }

    [Fact]
    public void AnswerTrivia_AdvancesCurrentStageOrderToNextStageOrder()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Delta").Value);

        team.AnswerTrivia(isCorrect: true, scoreChange: 30, nextStageOrder: 3);

        team.CurrentStageOrder.Should().Be(3);
    }

    [Fact]
    public void AnswerTrivia_ResetsCluesReceivedCurrentStageToZero()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Epsilon").Value);
        // Simulate clues received by using UpdateProgress
        team.UpdateProgress(stageOrder: 1, cluesCurrentStage: 2, totalClues: 5);

        team.AnswerTrivia(isCorrect: true, scoreChange: 30, nextStageOrder: 2);

        team.CluesReceivedCurrentStage.Should().Be(0);
    }

    [Fact]
    public void AnswerTrivia_SetsClueTimerResetAtToNonNull()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Zeta").Value);

        team.AnswerTrivia(isCorrect: true, scoreChange: 30, nextStageOrder: 2);

        team.ClueTimerResetAt.Should().NotBeNull();
    }

    // ── HU-21: tiempo de resolución (LastStageCompletedAt) ────────────────────

    [Fact]
    public void AnswerTrivia_CorrectAnswer_StampsLastStageCompletedAt()
    {
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Resolutor").Value);
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
        var team = Team.Create(Guid.NewGuid(), TeamName.Create("Errado").Value);

        team.AnswerTrivia(isCorrect: false, scoreChange: -50, nextStageOrder: 2);

        team.LastStageCompletedAt.Should().BeNull();
    }
}
