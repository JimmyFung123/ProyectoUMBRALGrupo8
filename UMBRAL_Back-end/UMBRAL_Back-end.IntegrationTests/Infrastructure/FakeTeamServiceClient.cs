// This file touches types that live in SessionService's aliased assembly (see the
// .csproj comment on the SessionService ProjectReference for why the alias exists).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using SessionServiceAssembly::SessionService.Application.Sessions;

/// <summary>
/// In-process substitute for <see cref="ITeamServiceClient"/> (SessionService → TeamService,
/// HTTP in production). SessionsController tests swap the real HTTP-backed implementation for
/// this via DI so cross-service calls never leave the process — no need for an
/// UpstreamJsonStub/DownstreamStub-style HTTP stub for a clean, already-injectable interface.
/// Every member defaults to a permissive/successful value so a test that doesn't care about a
/// particular cross-service call can ignore it entirely.
/// </summary>
public class FakeTeamServiceClient : ITeamServiceClient
{
    public bool HasEnrolledTeams { get; set; } = true;
    public bool AllTeamsMeetMinimumMembers { get; set; } = true;
    public int ReleaseClueResult { get; set; } = 1;
    public IReadOnlyList<TeamProgressItem> TeamProgressResult { get; set; } = [];
    public int PenalizeTeamResult { get; set; } = 100;
    public StageTransitionResult? ForceAdvanceResult { get; set; } = new(NewScore: 100, ElapsedSeconds: 30);
    public StageTransitionResult? RecordEvidenceOutcomeResult { get; set; } = new(NewScore: 100, ElapsedSeconds: 30);
    public WrongAttemptResult? RecordWrongAttemptResult { get; set; } = new(NewWrongCount: 1, NewScore: 90);
    public TeamInfoItem? TeamInfoResult { get; set; }
    public SessionRankingSnapshot? SessionRankingResult { get; set; }

    public Task<bool> HasEnrolledTeamsAsync(Guid sessionId, CancellationToken cancellationToken)
        => Task.FromResult(HasEnrolledTeams);

    public Task<int> ReleaseClueAsync(Guid teamId, int totalCluesForStage, CancellationToken cancellationToken, bool isAutomatic = false)
        => Task.FromResult(ReleaseClueResult);

    public Task<IReadOnlyList<TeamProgressItem>> GetTeamProgressAsync(Guid sessionId, CancellationToken cancellationToken)
        => Task.FromResult(TeamProgressResult);

    public Task<int> PenalizeTeamAsync(Guid teamId, int points, string reason, CancellationToken cancellationToken)
        => Task.FromResult(PenalizeTeamResult);

    public Task LeaveTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<StageTransitionResult?> ForceAdvanceTeamAsync(Guid teamId, int nextStageOrder, CancellationToken cancellationToken)
        => Task.FromResult(ForceAdvanceResult);

    public Task<StageTransitionResult?> RecordEvidenceOutcomeAsync(Guid teamId, bool isCorrect, int scoreChange, int nextStageOrder, CancellationToken cancellationToken)
        => Task.FromResult(RecordEvidenceOutcomeResult);

    public Task<bool> AllTeamsMeetMinimumMembersAsync(Guid sessionId, int minMembers, CancellationToken cancellationToken)
        => Task.FromResult(AllTeamsMeetMinimumMembers);

    public Task<WrongAttemptResult?> RecordWrongAttemptAsync(Guid teamId, Guid blockedOptionId, int scorePenalty, CancellationToken cancellationToken)
        => Task.FromResult(RecordWrongAttemptResult);

    public Task<TeamInfoItem?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken)
        => Task.FromResult(TeamInfoResult);

    public Task<SessionRankingSnapshot?> GetSessionRankingAsync(Guid sessionId, CancellationToken cancellationToken)
        => Task.FromResult(SessionRankingResult);
}
