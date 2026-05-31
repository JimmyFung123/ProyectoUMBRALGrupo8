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

        if (stage.Type == StageType.TreasureHunt && !string.IsNullOrWhiteSpace(request.QrCode))
            if (await _stageRepository.ExistsWithQrCodeAsync(request.QrCode.Trim(), excludeId: stage.Id, cancellationToken: cancellationToken))
                return Result.Failure<bool>(StageErrors.DuplicateQrCode);

        var updateResult = stage.Update(
            request.Title, request.Order, request.BaseScore,
            request.Question,
            request.Latitude, request.Longitude, request.QrCode,
            request.AutoReleaseTimeMinutes, request.AutoReleaseMaxAttempts);
        if (updateResult.IsFailure)
            return Result.Failure<bool>(updateResult.Error);

        if (stage.Type == StageType.Trivia && request.Options is not null)
        {
            // Delete old options directly in the DB (bypasses EF change tracker).
            await _stageRepository.RemoveOptionsAsync(stage.Id, cancellationToken);

            // Insert new options directly to the DbSet — avoids touching
            // stage.Options (which would call Options.Clear() and trigger EF's
            // change-tracker reconciliation on detached entities, causing the
            // FK violation 500).
            var newOptions = request.Options
                .Select(o => new TriviaOption(Guid.NewGuid(), stage.Id, o.Text, o.IsCorrect));
            await _stageRepository.AddOptionsAsync(newOptions, cancellationToken);
        }

        await _stageRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
