namespace SessionService.Application.SyncHealth.Commands.ReconcileStageCompletionRecords;

using MediatR;
using SessionService.Application.SyncHealth;
using SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

public class ReconcileStageCompletionRecordsCommandHandler
    : IRequestHandler<ReconcileStageCompletionRecordsCommand, ReprojectActionResultDto>
{
    private readonly ILocalSyncHealthReader _reader;
    public ReconcileStageCompletionRecordsCommandHandler(ILocalSyncHealthReader reader) => _reader = reader;

    public async Task<ReprojectActionResultDto> Handle(
        ReconcileStageCompletionRecordsCommand request,
        CancellationToken cancellationToken)
    {
        var changed = await _reader.ReconcileStageCompletionRecordsAsync(cancellationToken);
        return new ReprojectActionResultDto(
            ProjectionId: "stage-completion-records",
            Success: true,
            ChangedRows: changed,
            Detail: $"Reconciled {changed} StageCompletionRecord rows against session status.",
            CompletedAt: DateTime.UtcNow);
    }
}
