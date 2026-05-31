namespace SessionService.Application.Sessions.Commands.Evidence;

/// <summary>
/// Carries the result of the evidence-processing hook inside
/// <see cref="EvidenceHandlerBase{TCommand,TResultDto}"/>.
/// </summary>
/// <param name="IsCorrect">Whether the evidence was valid.</param>
/// <param name="ScoreChange">Signed score delta resolved by IScoringStrategy.</param>
/// <param name="ShouldAdvance">
/// False when the evidence was rejected and the team must stay on the current
/// stage (e.g. wrong QR scan). The template will build an early result and
/// return without calling TeamService or SignalR.
/// </param>
/// <param name="TeamCurrentStageOrder">
/// Only populated when <see cref="ShouldAdvance"/> is false — used to build
/// the early result DTO with the team's unchanged stage order.
/// </param>
public record EvidenceOutcome(
    bool IsCorrect,
    int ScoreChange,
    bool ShouldAdvance,
    int TeamCurrentStageOrder = 0);
