namespace TeamService.Domain.Teams;

/// <summary>
/// Result of a stage transition on the <see cref="Team"/> aggregate (HU-25).
///
/// Carries the post-transition score (already used by HU-15/HU-16/HU-18) plus
/// the seconds the team spent on the stage they just left — required by
/// SessionService to write the analytics fact row for the historical
/// statistics dashboard.
/// </summary>
/// <param name="NewScore">The team's score after the transition.</param>
/// <param name="ElapsedSeconds">
/// Seconds elapsed since the team entered the stage being abandoned.
/// Zero when no stage start time was recorded (e.g. legacy pre-HU-25 rows).
/// </param>
public record StageTransitionOutcome(int NewScore, int ElapsedSeconds);
