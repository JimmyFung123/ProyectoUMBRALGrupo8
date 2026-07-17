namespace SessionService.Application.Sessions.Scoring;

/// <summary>
/// Hard difficulty: 150% of base score on correct answer, full penalty on wrong answer.
/// High risk, high reward — guessing is heavily discouraged.
/// </summary>
public class HardScoringStrategy : IScoringStrategy
{
    public int Calculate(int baseScore, bool isCorrect)
        => isCorrect ? (int)(baseScore * 1.5) : -baseScore;
}
