namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissionById;

using MediatR;
using UMBRAL_Back_end.Application.Missions.Queries.GetMissions;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class GetMissionByIdQueryHandler : IRequestHandler<GetMissionByIdQuery, Result<MissionDto>>
{
    private readonly IMissionRepository _repository;

    public GetMissionByIdQueryHandler(IMissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<MissionDto>> Handle(GetMissionByIdQuery request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure<MissionDto>(MissionErrors.NotFound);

        var dto = new MissionDto(
            mission.Id,
            mission.Name,
            mission.Description,
            mission.Difficulty.ToString(),
            mission.MaxDuration,
            mission.Status.ToString(),
            mission.Stages.Count,
            mission.CreatedAt,
            mission.UpdatedAt);

        return Result.Success(dto);
    }
}
