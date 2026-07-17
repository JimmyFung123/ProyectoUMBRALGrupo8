namespace StageService.Application.Stages.Queries.GetStageById;

using MediatR;
using StageService.Application.Stages.Queries.GetStagesByMission;
using StageService.Domain.Common;
using StageService.Domain.Stages;

public class GetStageByIdQueryHandler : IRequestHandler<GetStageByIdQuery, Result<StageDto>>
{
    private readonly IStageRepository _repository;

    public GetStageByIdQueryHandler(IStageRepository repository)
        => _repository = repository;

    public async Task<Result<StageDto>> Handle(GetStageByIdQuery request, CancellationToken cancellationToken)
    {
        var stage = await _repository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null)
            return Result.Failure<StageDto>(StageErrors.NotFound);

        return Result.Success(new StageDto(
            stage.Id, stage.MissionId, stage.Title, stage.Type.ToString(), stage.Order, stage.BaseScore,
            stage.Question,
            stage.Options.Select(o => new TriviaOptionDto(o.Id, o.Text, o.IsCorrect)).ToList(),
            stage.Latitude, stage.Longitude, stage.QrCode,
            stage.AutoReleaseTimeMinutes, stage.AutoReleaseMaxAttempts,
            stage.CreatedAt));
    }
}
