namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissionById;

using MediatR;
using UMBRAL_Back_end.Application.Missions.Queries.GetMissions;
using UMBRAL_Back_end.Domain.Common;

public record GetMissionByIdQuery(Guid MissionId) : IRequest<Result<MissionDto>>;
