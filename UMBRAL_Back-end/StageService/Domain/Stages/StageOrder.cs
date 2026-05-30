namespace StageService.Domain.Stages;

using StageService.Domain.Common;

/// <summary>
/// Value Object for a stage's sequential position within a mission (Mission
/// Design context, HU-2). Stages are presented to participants in this order;
/// must be a positive integer (1-based).
/// </summary>
public sealed record StageOrder
{
    public int Value { get; }

    private StageOrder(int value) => Value = value;

    public static Result<StageOrder> Create(int value)
    {
        if (value <= 0)
            return Result.Failure<StageOrder>(StageErrors.InvalidOrder);

        return Result.Success(new StageOrder(value));
    }

    public override string ToString() => Value.ToString();
}
