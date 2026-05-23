namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissions;

using MediatR;

public record GetMissionsQuery : IRequest<IReadOnlyList<MissionDto>>;
