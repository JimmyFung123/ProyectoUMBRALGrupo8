namespace UMBRAL_Back_end.Application.Missions.Commands.AddClue;

using MediatR;
using UMBRAL_Back_end.Domain.Common;

public record AddClueCommand(
    Guid MissionId,
    Guid StageId,
    int Order,
    string? Content,
    double? Latitude,
    double? Longitude,
    double? RadiusMeters
) : IRequest<Result<Guid>>;
