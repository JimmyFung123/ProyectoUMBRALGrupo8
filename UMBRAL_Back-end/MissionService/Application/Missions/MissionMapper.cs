namespace UMBRAL_Back_end.Application.Missions;

using UMBRAL_Back_end.Application.Missions.Queries.GetMissions;
using UMBRAL_Back_end.Domain.Missions;

public static class MissionMapper
{
    public static MissionDto ToDto(Mission m) => new(
        m.Id,
        m.Name,
        m.Description,
        m.Difficulty.ToString(),
        m.MaxDuration,
        m.Status.ToString(),
        m.CreatedAt,
        m.UpdatedAt);
}
