namespace SessionService.Application.Sessions.Queries.GetParticipantStage;

public record ParticipantOptionDto(Guid Id, string Text);

public record ParticipantStageDto(
    Guid StageId,
    string Title,
    string Type,
    int Order,
    string? Question,
    IReadOnlyList<ParticipantOptionDto> Options,
    string SessionStatus,
    int CurrentStageOrder,
    bool IsLastStage,
    double? Latitude = null,
    double? Longitude = null);
