namespace StageService.Application.Stages.Queries.GetStagesByMission;

public record TriviaOptionDto(Guid Id, string Text, bool IsCorrect);

public record StageDto(
    Guid Id,
    Guid MissionId,
    string Title,
    string Type,
    int Order,
    int BaseScore,
    string? Question,
    List<TriviaOptionDto> Options,
    double? Latitude,
    double? Longitude,
    string? QrCode,
    int? AutoReleaseTimeMinutes,
    int? AutoReleaseMaxAttempts,
    DateTime CreatedAt
);
