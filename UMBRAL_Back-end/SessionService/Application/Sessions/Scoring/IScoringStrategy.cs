namespace SessionService.Application.Sessions.Scoring;

/// <summary>
/// Strategy pattern — defines how points are awarded or deducted for a trivia
/// answer based on the mission's difficulty level.
/// </summary>
public interface IScoringStrategy
{
    /// <summary>
    /// Calculates the score change to apply to the team.
    /// A positive value means points are added; a negative value means
    /// points are deducted as a penalty.
    /// </summary>
    /// <param name="baseScore">The stage's configured base score.</param>
    /// <param name="isCorrect">Whether the team answered correctly.</param>
    /// <returns>Points to add (positive) or subtract (negative) from the team score.</returns>
    int Calculate(int baseScore, bool isCorrect);
}
