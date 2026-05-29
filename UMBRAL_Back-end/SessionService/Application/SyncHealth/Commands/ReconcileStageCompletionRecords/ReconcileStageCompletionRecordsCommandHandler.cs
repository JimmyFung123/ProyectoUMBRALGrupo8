namespace SessionService.Application.SyncHealth.Commands.ReconcileStageCompletionRecords;

using MediatR;
using SessionService.Application.SyncHealth;
using SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

public class ReconcileStageCompletionRecordsCommandHandler
    : IRequestHandler<ReconcileStageCompletionRecordsCommand, ReprojectActionResultDto>
{
    private readonly ILocalSyncHealthReader _localReader;

    public ReconcileStageCompletionRecordsCommandHandler(ILocalSyncHealthReader localReader)
        => _localReader = localReader;

    public async Task<ReprojectActionResultDto> Handle(
        ReconcileStageCompletionRecordsCommand request,
        CancellationToken ct)
    {
        var fixedRows = await _localReader.ReconcileStageCompletionRecordsAsync(ct);
        return new ReprojectActionResultDto(
            ProjectionId: "stage-completion-records",
            Success: true,
            ChangedRows: fixedRows,
            Detail: fixedRows == 0
                ? "Flag IncludedInStatistics ya consistente con el estado de cada sesión."
                : $"{fixedRows} fila(s) corregidas: ahora IncludedInStatistics refleja el estado de la sesión.",
            CompletedAt: DateTime.UtcNow);
    }
}
