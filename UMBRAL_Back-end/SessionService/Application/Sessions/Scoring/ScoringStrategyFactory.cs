namespace SessionService.Application.Sessions.Scoring;

/// <summary>
/// Factory that resolves the correct <see cref="IScoringStrategy"/> based on
/// a mission's difficulty level string (as stored in MissionLookup).
/// Defaults to <see cref="MediumScoringStrategy"/> for unknown values.
/// </summary>
public static class ScoringStrategyFactory
{
    public static IScoringStrategy Create(string difficulty) => difficulty switch
    {
        "Easy"   => new EasyScoringStrategy(),
        "Hard"   => new HardScoringStrategy(),
        _        => new MediumScoringStrategy()   // Medium or any unknown value
    };
}
