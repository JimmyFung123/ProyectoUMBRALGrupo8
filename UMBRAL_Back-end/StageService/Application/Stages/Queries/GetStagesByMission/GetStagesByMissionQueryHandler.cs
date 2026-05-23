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
        return stages.Select(s => new StageDto(
            s.Id, s.MissionId, s.Title, s.Type.ToString(), s.Order, s.BaseScore,
            s.Question,
            s.Options.Select(o => new TriviaOptionDto(o.Id, o.Text, o.IsCorrect)).ToList(),
            s.Latitude, s.Longitude, s.QrCode,
            s.AutoReleaseTimeMinutes, s.AutoReleaseMaxAttempts,
            s.CreatedAt
        )).ToList();
    }
}
