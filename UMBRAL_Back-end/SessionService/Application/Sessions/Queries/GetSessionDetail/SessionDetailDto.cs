namespace SessionService.Application.Sessions.Queries.GetSessionDetail;

public record SessionDetailDto(
    Guid Id,
    Guid MissionId,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? ScheduledAt);
