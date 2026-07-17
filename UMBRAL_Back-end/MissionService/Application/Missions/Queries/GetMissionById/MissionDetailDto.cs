namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissionById;

public record MissionDetailDto(
    Guid Id,
    string Name,
    string Description,
    string Difficulty,
    int MaxDuration,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
