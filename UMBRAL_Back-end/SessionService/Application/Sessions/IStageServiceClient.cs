namespace SessionService.Application.Sessions;

public interface IStageServiceClient
{
    Task<IReadOnlyList<StageInfo>> GetStagesByMissionAsync(Guid missionId, CancellationToken ct);

    /// <summary>
    /// Returns a stage with full option details (including IsCorrect).
    /// Only SessionService may call this — participants never see IsCorrect directly.
    /// </summary>
    Task<StageWithOptionsInfo?> GetStageWithOptionsAsync(Guid stageId, CancellationToken ct);
}

public record StageInfo(Guid Id, int Order);

public record StageWithOptionsInfo(
    Guid Id,
    string Title,
    string Type,
    int Order,
    int BaseScore,
    string? Question,
    IReadOnlyList<TriviaOptionInfo> Options);

public record TriviaOptionInfo(Guid Id, string Text, bool IsCorrect);
