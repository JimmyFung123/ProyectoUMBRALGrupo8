namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissions;

using MediatR;

public record GetMissionsQuery(string? Status = null) : IRequest<IReadOnlyList<MissionDto>>;
