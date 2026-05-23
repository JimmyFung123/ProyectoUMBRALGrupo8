namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissionById;

public record TriviaOptionDetailDto(Guid Id, string Text, bool IsCorrect);

public record StageDetailDto(
    Guid Id,
    string Title,
    int Order,
    string Type,
    int BaseScore,
    string? Question,
    IReadOnlyList<TriviaOptionDetailDto> Options,
    double? Latitude,
    double? Longitude,
    string? QrCode,
    int? AutoReleaseTimeMinutes,
    int? AutoReleaseMaxAttempts);

public record MissionDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Difficulty,
    int MaxDuration,
    string Status,
    IReadOnlyList<StageDetailDto> Stages,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
