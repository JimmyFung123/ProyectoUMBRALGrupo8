namespace SessionService.Application.Sessions.Scoring;

/// <summary>
/// Medium difficulty: full points on correct answer, half the base score as
/// penalty on wrong answer. Balanced risk/reward.
/// </summary>
public class MediumScoringStrategy : IScoringStrategy
{
    public int Calculate(int baseScore, bool isCorrect)
        => isCorrect ? baseScore : -(baseScore / 2);
}
