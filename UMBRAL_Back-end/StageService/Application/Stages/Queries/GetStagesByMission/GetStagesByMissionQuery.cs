namespace StageService.Application.Stages.Queries.GetStagesByMission;
using MediatR;
public record GetStagesByMissionQuery(Guid MissionId) : IRequest<List<StageDto>>;
