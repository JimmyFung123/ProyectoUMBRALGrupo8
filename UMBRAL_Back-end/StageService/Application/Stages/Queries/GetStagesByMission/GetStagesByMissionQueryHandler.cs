namespace StageService.Application.Stages.Queries.GetStagesByMission;
using MediatR;
using StageService.Domain.Stages;
public class GetStagesByMissionQueryHandler : IRequestHandler<GetStagesByMissionQuery, List<StageDto>>
{
    private readonly IStageRepository _repository;
    public GetStagesByMissionQueryHandler(IStageRepository repository) => _repository = repository;

    public async Task<List<StageDto>> Handle(GetStagesByMissionQuery request, CancellationToken cancellationToken)
    {
        var stages = await _repository.GetByMissionIdAsync(request.MissionId, cancellationToken);
        return stages.Select(StageMapper.ToDto).ToList();
    }
}
