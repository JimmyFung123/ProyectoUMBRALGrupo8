namespace SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

using MediatR;
using SessionService.Application.SyncHealth;

public class ReprojectSessionMissionsLookupCommandHandler
    : IRequestHandler<ReprojectSessionMissionsLookupCommand, ReprojectActionResultDto>
{
    private readonly IMissionServiceSyncClient _client;
    private readonly ILocalSyncHealthReader _reader;

    public ReprojectSessionMissionsLookupCommandHandler(
        IMissionServiceSyncClient client,
        ILocalSyncHealthReader reader)
    {
        _client = client;
        _reader = reader;
    }

    public async Task<ReprojectActionResultDto> Handle(
        ReprojectSessionMissionsLookupCommand request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _client.GetSnapshotAsync(cancellationToken);
        if (snapshot is null)
            return new ReprojectActionResultDto(
                ProjectionId: "missions-lookup-session",
                Success: false,
                ChangedRows: 0,
                Detail: "MissionService is unreachable. MissionsLookup table was not modified.",
                CompletedAt: DateTime.UtcNow);

        var changed = await _reader.ReprojectMissionsLookupAsync(
            ct => _client.GetMissionsAsync(ct),
            cancellationToken);

        return new ReprojectActionResultDto(
            ProjectionId: "missions-lookup-session",
            Success: true,
            ChangedRows: changed,
            Detail: $"SessionService rebuilt MissionsLookup from MissionService ({changed} rows affected).",
            CompletedAt: DateTime.UtcNow);
    }
}
