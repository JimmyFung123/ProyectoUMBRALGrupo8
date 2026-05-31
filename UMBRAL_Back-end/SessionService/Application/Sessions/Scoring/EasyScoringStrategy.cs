namespace SessionService.Application.Sessions.Scoring;

/// <summary>
/// Easy difficulty: 75% of base score on correct answer, no penalty on wrong answer.
/// Encourages participation without risk.
/// </summary>
public class EasyScoringStrategy : IScoringStrategy
{
    public int Calculate(int baseScore, bool isCorrect)
        => isCorrect ? (int)(baseScore * 0.75) : 0;
}
