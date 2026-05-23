namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissions;

public record MissionDto(
    Guid Id,
    string Name,
    string Description,
    string Difficulty,
    int MaxDuration,
    string Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
