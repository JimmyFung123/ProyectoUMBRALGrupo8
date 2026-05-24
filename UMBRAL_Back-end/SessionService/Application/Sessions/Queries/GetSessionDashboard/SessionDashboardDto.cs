namespace SessionService.Application.Sessions.Queries.GetSessionDashboard;

public record SessionDashboardDto(
    Guid Id,
    Guid MissionId,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? ScheduledAt,
    IReadOnlyList<SessionEventDto> RecentEvents
);

public record SessionEventDto(Guid Id, string Description, DateTime OccurredAt);
