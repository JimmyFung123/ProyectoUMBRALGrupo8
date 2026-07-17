namespace SessionService.Application.Sessions.Queries.GetReleasedClues;

public record ReleasedClueDto(
    Guid Id,
    int Order,
    string? Content,
    double? Latitude,
    double? Longitude,
    int? RadiusMeters);

public record ReleasedCluesDto(
    Guid StageId,
    int StageOrder,
    string StageType,
    int CluesReceived,
    int TotalCluesForStage,
    IReadOnlyList<ReleasedClueDto> Clues);
