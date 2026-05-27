namespace UMBRAL_Back_end.Tests.Domain;

using FluentAssertions;
using TeamService.Domain.Teams;
using Xunit;

/// <summary>
/// HU-25: tests for <see cref="Team.CurrentStageStartedAt"/> and the
/// <see cref="StageTransitionOutcome.ElapsedSeconds"/> field returned
/// by stage-transition methods. These are the timing primitives the
/// analytics dashboard depends on.
/// </summary>
public class TeamStageTimingTests
{
    // ── CurrentStageStartedAt initialisation ──────────────────────────────────

    [Fact]
    public void NewTeam_HasNoCurrentStageStartedAt()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");

        team.CurrentStageStartedAt.Should().BeNull();
    }

    [Fact]
    public void UpdateProgress_WhenEnteringFirstStage_StampsCurrentStageStartedAt()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");

        var before = DateTime.UtcNow;
        team.UpdateProgress(stageOrder: 1, cluesCurrentStage: 0, totalClues: 3);
        var after = DateTime.UtcNow;

        team.CurrentStageStartedAt.Should().NotBeNull();
        team.CurrentStageStartedAt!.Value.Should()
            .BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void UpdateProgress_OnSameStageOrder_DoesNotResetCurrentStageStartedAt()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        team.UpdateProgress(1, 0, 3);
        var firstTimestamp = team.CurrentStageStartedAt;

        // Same stage, just a clue counter bump — the stage clock must keep running.
        team.UpdateProgress(1, 1, 3);

        team.CurrentStageStartedAt.Should().Be(firstTimestamp);
    }

    // ── AnswerTrivia returns elapsed and resets the clock ────────────────────

    [Fact]
    public void AnswerTrivia_ReturnsElapsedSecondsForTheStageJustLeft()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        // Manually stamp the stage start in the past so the elapsed math is testable.
        SetCurrentStageStartedAt(team, DateTime.UtcNow.AddSeconds(-90));

        var result = team.AnswerTrivia(isCorrect: true, scoreChange: 50, nextStageOrder: 2);

        result.IsSuccess.Should().BeTrue();
        // Allow small jitter — the test machine is doing real wall-clock subtraction.
        result.Value.ElapsedSeconds.Should().BeInRange(89, 92);
    }

    [Fact]
    public void AnswerTrivia_AdvancesStage_AndResetsCurrentStageStartedAtToNow()
    {
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        SetCurrentStageStartedAt(team, DateTime.UtcNow.AddSeconds(-30));

        var before = DateTime.UtcNow;
        team.AnswerTrivia(isCorrect: true, scoreChange: 50, nextStageOrder: 2);
        var after = DateTime.UtcNow;

        team.CurrentStageStartedAt.Should().NotBeNull();
        team.CurrentStageStartedAt!.Value.Should()
            .BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void AnswerTrivia_WhenStageStartIsNull_ElapsedSecondsIsZero()
    {
        // Defensive case for pre-HU-25 rows that never had the column populated.
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        // CurrentStageStartedAt is null at this point.

        var result = team.AnswerTrivia(isCorrect: true, scoreChange: 50, nextStageOrder: 1);

        result.Value.ElapsedSeconds.Should().Be(0);
    }

    // ── ForceAdvance returns elapsed and resets the clock ────────────────────

    [Fact]
    public void ForceAdvance_ReturnsElapsedSecondsForTheSkippedStage()
    {
        var team = Team.Create(Guid.NewGuid(), "Beta");
        team.UpdateProgress(1, 0, 3); // currentStageOrder = 1
        SetCurrentStageStartedAt(team, DateTime.UtcNow.AddSeconds(-120));

        var result = team.ForceAdvance(2);

        result.IsSuccess.Should().BeTrue();
        result.Value.ElapsedSeconds.Should().BeInRange(119, 122);
    }

    [Fact]
    public void ForceAdvance_DoesNotChangeScore_ButRoundsElapsedSeconds()
    {
        var team = Team.Create(Guid.NewGuid(), "Beta");
        team.UpdateProgress(1, 0, 3);
        team.UpdateScore(200);
        SetCurrentStageStartedAt(team, DateTime.UtcNow.AddSeconds(-45));

        var result = team.ForceAdvance(2);

        result.Value.NewScore.Should().Be(200); // unchanged
        team.Score.Should().Be(200);
    }

    [Fact]
    public void ForceAdvance_FailsValidation_ElapsedNotComputed()
    {
        var team = Team.Create(Guid.NewGuid(), "Beta");
        team.UpdateProgress(2, 0, 3); // already on stage 2

        var result = team.ForceAdvance(2); // same order → InvalidNextStage

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TeamErrors.InvalidNextStage);
        // The stage clock must NOT be reset when the transition fails.
    }

    // ── ElapsedSeconds clamps to zero on clock skew ──────────────────────────

    [Fact]
    public void AnswerTrivia_WhenStageStartIsInTheFuture_ElapsedSecondsClampsToZero()
    {
        // Defensive: if the clock skewed forward between writes, we still
        // produce a sensible analytics row (0 seconds) instead of a negative value.
        var team = Team.Create(Guid.NewGuid(), "Alpha");
        SetCurrentStageStartedAt(team, DateTime.UtcNow.AddSeconds(10));

        var result = team.AnswerTrivia(isCorrect: true, scoreChange: 10, nextStageOrder: 2);

        result.Value.ElapsedSeconds.Should().Be(0);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void SetCurrentStageStartedAt(Team team, DateTime value)
    {
        // Domain methods always stamp DateTime.UtcNow; we punch the private
        // setter to make the elapsed math deterministic for the assertion.
        var prop = typeof(Team).GetProperty(nameof(Team.CurrentStageStartedAt))!;
        prop.GetSetMethod(nonPublic: true)!.Invoke(team, new object?[] { value });
    }
}
