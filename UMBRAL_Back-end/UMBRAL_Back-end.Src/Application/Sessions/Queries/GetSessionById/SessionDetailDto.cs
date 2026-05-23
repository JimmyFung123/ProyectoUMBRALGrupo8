namespace UMBRAL_Back_end.Application.Sessions.Queries.GetSessionById;

public record SessionDetailDto(
    Guid Id,
    Guid MissionId,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? ScheduledAt);
