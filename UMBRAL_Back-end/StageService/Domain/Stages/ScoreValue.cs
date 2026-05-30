namespace StageService.Domain.Stages;

using StageService.Domain.Common;

/// <summary>
/// Value Object for a stage's base score (Mission Design context, HU-2). The
/// points a team earns for resolving the stage; cannot be negative.
/// </summary>
public sealed record ScoreValue
{
    public int Points { get; }

    private ScoreValue(int points) => Points = points;

    public static Result<ScoreValue> Create(int points)
    {
        if (points < 0)
            return Result.Failure<ScoreValue>(StageErrors.InvalidBaseScore);

        return Result.Success(new ScoreValue(points));
    }

    public override string ToString() => Points.ToString();
}
