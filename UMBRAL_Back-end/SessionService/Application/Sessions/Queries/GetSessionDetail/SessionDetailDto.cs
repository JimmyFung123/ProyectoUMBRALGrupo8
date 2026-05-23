namespace SessionService.Application.Sessions.Queries.GetSessionDetail;

public record TeamDto(
    Guid Id,
    string Name,
    bool IsConnected,
    int CurrentStageOrder,
    int CluesReceivedCurrentStage,
    int TotalCluesReceived);

public record SessionDetailDto(
    Guid Id,
    Guid MissionId,
    string Name,
    string Status,
    DateTime CreatedAt,
    DateTime? ScheduledAt,
    IReadOnlyList<TeamDto> Teams);
