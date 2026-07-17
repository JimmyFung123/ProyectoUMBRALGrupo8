namespace SessionService.Application.SyncHealth.Commands.ReconcileStageCompletionRecords;

using MediatR;
using SessionService.Application.SyncHealth.Commands.ReprojectSessionMissionsLookup;

/// <summary>
/// HU-27 — re-checks the visibility flag of every <c>StageCompletionRecord</c>
/// against the actual status of its parent session. The flag should be
/// <c>true</c> for finalized sessions and <c>false</c> for everything else;
/// any row that breaks that invariant is fixed in place.
/// </summary>
public record ReconcileStageCompletionRecordsCommand() : IRequest<ReprojectActionResultDto>;
