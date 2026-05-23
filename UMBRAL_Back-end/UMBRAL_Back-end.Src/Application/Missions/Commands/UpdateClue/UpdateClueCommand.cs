namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateClue;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record UpdateClueCommand(
    Guid MissionId,
    Guid StageId,
    Guid ClueId,
    int Order,
    string? Content,
    double? Latitude,
    double? Longitude,
    double? RadiusMeters
) : IRequest<Result>;
