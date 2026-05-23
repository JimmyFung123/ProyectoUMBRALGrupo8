namespace UMBRAL_Back_end.Application.Missions.Queries.GetCluesByStage;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class GetCluesByStageQueryHandler : IRequestHandler<GetCluesByStageQuery, Result<IReadOnlyList<ClueDto>>>
{
    private readonly IMissionRepository _repository;

    public GetCluesByStageQueryHandler(IMissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<ClueDto>>> Handle(GetCluesByStageQuery request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure<IReadOnlyList<ClueDto>>(MissionErrors.NotFound);

        var stage = mission.Stages.FirstOrDefault(s => s.Id == request.StageId);
        if (stage is null)
            return Result.Failure<IReadOnlyList<ClueDto>>(StageErrors.NotFound);

        var clues = stage.Clues
            .OrderBy(c => c.Order)
            .Select(c => new ClueDto(c.Id, c.Order, c.Content, c.Latitude, c.Longitude, c.RadiusMeters))
            .ToList();

        return Result.Success<IReadOnlyList<ClueDto>>(clues);
    }
}
