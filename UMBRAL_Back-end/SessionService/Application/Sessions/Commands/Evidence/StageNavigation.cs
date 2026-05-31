namespace SessionService.Application.Sessions.Commands.Evidence;

/// <summary>
/// Navigation data computed by the template method after a successful evidence
/// submission: where the team moves next and whether the mission is complete.
/// </summary>
public record StageNavigation(int NextStageOrder, bool IsLastStage);
