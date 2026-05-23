namespace UMBRAL_Back_end.Domain.Missions.Events;

using MediatR;

/// <summary>Published after an auto-release rule is configured on a stage.</summary>
public record ClueReleaseRuleConfiguredEvent(
    Guid MissionId,
    Guid StageId,
    int? TimeMinutes,
    int? MaxAttempts,
    DateTime OccurredAt
) : INotification;
