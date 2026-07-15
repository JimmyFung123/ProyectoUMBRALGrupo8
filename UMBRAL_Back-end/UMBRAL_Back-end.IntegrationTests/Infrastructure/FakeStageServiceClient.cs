// This file touches types that live in SessionService's aliased assembly (see the
// .csproj comment on the SessionService ProjectReference for why the alias exists).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using SessionServiceAssembly::SessionService.Application.Sessions;

/// <summary>
/// In-process substitute for <see cref="IStageServiceClient"/> (SessionService → StageService,
/// HTTP in production, same rationale as <see cref="FakeTeamServiceClient"/>). Defaults to a
/// single Trivia stage so evidence-handler tests (SubmitTriviaAnswer/ValidateQr/
/// GetParticipantStage) don't need to configure a stage list just to get past the
/// "no stages configured" guard — only <see cref="StageWithOptionsResult"/> needs setting for
/// tests that exercise the actual evidence-validation hook.
/// </summary>
public class FakeStageServiceClient : IStageServiceClient
{
    public IReadOnlyList<StageInfo> StagesResult { get; set; } = [new StageInfo(Guid.NewGuid(), Order: 1)];
    public StageWithOptionsInfo? StageWithOptionsResult { get; set; }

    public Task<IReadOnlyList<StageInfo>> GetStagesByMissionAsync(Guid missionId, CancellationToken ct)
        => Task.FromResult(StagesResult);

    public Task<StageWithOptionsInfo?> GetStageWithOptionsAsync(Guid stageId, CancellationToken ct)
        => Task.FromResult(StageWithOptionsResult);
}
