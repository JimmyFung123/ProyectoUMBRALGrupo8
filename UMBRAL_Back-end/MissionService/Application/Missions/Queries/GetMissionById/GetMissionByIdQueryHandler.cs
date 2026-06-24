namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissionById;

using MediatR;
using UMBRAL_Back_end.Application.Missions;
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

        return Result.Success(MissionMapper.ToDto(mission));
    }
}
