namespace UMBRAL_Back_end.Application.Missions.Queries.GetCluesByStage;

public record ClueDto(
    Guid Id,
    int Order,
    string? Content,
    double? Latitude,
    double? Longitude,
    double? RadiusMeters);
