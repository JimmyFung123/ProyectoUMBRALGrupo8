namespace SessionService.Application.Sessions;

public interface IStageServiceClient
{
    Task<IReadOnlyList<StageInfo>> GetStagesByMissionAsync(Guid missionId, CancellationToken ct);
}

public record StageInfo(Guid Id, int Order);
