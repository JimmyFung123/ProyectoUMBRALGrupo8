namespace ClueService.Domain.Clues.Events;

using ClueService.Domain.Common;

public record ClueAddedDomainEvent(
    Guid ClueId,
    Guid StageId,
    Guid MissionId,
    string? Content,
    double? Latitude,
    double? Longitude,
    int? RadiusMeters,
    DateTime OccurredAt
) : IDomainEvent;
