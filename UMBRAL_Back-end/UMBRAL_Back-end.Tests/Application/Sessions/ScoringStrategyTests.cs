namespace UMBRAL_Back_end.Tests.Application.Sessions;

using FluentAssertions;
using SessionService.Application.Sessions.Scoring;
using Xunit;

public class ScoringStrategyTests
{
    // ── EasyScoringStrategy ───────────────────────────────────────────────────

    [Fact]
    public void Easy_WhenCorrect_Returns75PercentOfBaseScore()
    {
        var strategy = new EasyScoringStrategy();
        strategy.Calculate(100, isCorrect: true).Should().Be(75);
    }

    [Fact]
    public void Easy_WhenWrong_ReturnsZero()
    {
        var strategy = new EasyScoringStrategy();
        strategy.Calculate(100, isCorrect: false).Should().Be(0);
    }

    // ── MediumScoringStrategy ─────────────────────────────────────────────────

    [Fact]
    public void Medium_WhenCorrect_ReturnsFullBaseScore()
    {
        var strategy = new MediumScoringStrategy();
        strategy.Calculate(100, isCorrect: true).Should().Be(100);
    }

    [Fact]
    public void Medium_WhenWrong_ReturnsHalfPenalty()
    {
        var strategy = new MediumScoringStrategy();
        strategy.Calculate(100, isCorrect: false).Should().Be(-50);
    }

    // ── HardScoringStrategy ───────────────────────────────────────────────────

    [Fact]
    public void Hard_WhenCorrect_Returns150PercentOfBaseScore()
    {
        var strategy = new HardScoringStrategy();
        strategy.Calculate(100, isCorrect: true).Should().Be(150);
    }

    [Fact]
    public void Hard_WhenWrong_ReturnsFullPenalty()
    {
        var strategy = new HardScoringStrategy();
        strategy.Calculate(100, isCorrect: false).Should().Be(-100);
    }

    // ── ScoringStrategyFactory ────────────────────────────────────────────────

    [Theory]
    [InlineData("Easy",   typeof(EasyScoringStrategy))]
    [InlineData("Medium", typeof(MediumScoringStrategy))]
    [InlineData("Hard",   typeof(HardScoringStrategy))]
    public void Factory_ReturnsCorrectStrategy(string difficulty, Type expectedType)
    {
        var strategy = ScoringStrategyFactory.Create(difficulty);
        strategy.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("")]
    [InlineData("EASY")]
    public void Factory_WhenUnknownDifficulty_DefaultsToMedium(string difficulty)
    {
        var strategy = ScoringStrategyFactory.Create(difficulty);
        strategy.Should().BeOfType<MediumScoringStrategy>();
    }
}
