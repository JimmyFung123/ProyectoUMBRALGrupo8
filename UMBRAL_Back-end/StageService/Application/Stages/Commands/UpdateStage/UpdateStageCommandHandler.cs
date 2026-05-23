namespace StageService.Application.Stages.Commands.UpdateStage;

using MediatR;
using StageService.Domain.Common;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;

public class UpdateStageCommandHandler : IRequestHandler<UpdateStageCommand, Result<bool>>
{
    private readonly IStageRepository _stageRepository;
    private readonly IMissionLookupRepository _missionLookupRepository;

    public UpdateStageCommandHandler(IStageRepository stageRepository, IMissionLookupRepository missionLookupRepository)
    {
        _stageRepository = stageRepository;
        _missionLookupRepository = missionLookupRepository;
    }

    public async Task<Result<bool>> Handle(UpdateStageCommand request, CancellationToken cancellationToken)
    {
        var stage = await _stageRepository.GetByIdAsync(request.StageId, cancellationToken);
        if (stage is null) return Result.Failure<bool>(StageErrors.NotFound);

        var mission = await _missionLookupRepository.GetByIdAsync(stage.MissionId, cancellationToken);
        if (mission is not null && mission.IsActive) return Result.Failure<bool>(StageErrors.MissionIsActive);

        stage.Update(
            request.Title, request.Order, request.BaseScore,
            request.Question,
            request.Latitude, request.Longitude, request.QrCode,
            request.AutoReleaseTimeMinutes, request.AutoReleaseMaxAttempts);

        if (stage.Type == StageType.Trivia && request.Options is not null)
            stage.ReplaceOptions(request.Options.Select(o => (o.Text, o.IsCorrect)));

        await _stageRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
