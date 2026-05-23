namespace UMBRAL_Back_end.Application.Sessions.Queries.GetSessions;

public record SessionDto(
    Guid Id,
    Guid MissionId,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? ScheduledAt);
