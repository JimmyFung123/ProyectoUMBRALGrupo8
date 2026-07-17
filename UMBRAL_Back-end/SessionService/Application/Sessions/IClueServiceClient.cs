namespace SessionService.Application.Sessions;

public interface IClueServiceClient
{
    Task<IReadOnlyList<ClueInfo>> GetCluesByStageAsync(Guid stageId, CancellationToken ct);
}

public record ClueInfo(
    Guid Id,
    int Order,
    string? Content,
    double? Latitude,
    double? Longitude,
    int? RadiusMeters,
    int? AutoReleaseAfterMinutes);
