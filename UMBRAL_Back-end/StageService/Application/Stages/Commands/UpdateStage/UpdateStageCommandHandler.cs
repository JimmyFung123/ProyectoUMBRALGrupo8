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
        {
            // Fix del 500 al editar una trivia: borramos las opciones viejas
            // directamente en SQL (ExecuteDelete) y luego dejamos que el
            // dominio adjunte las nuevas. Esto evita el caso en que EF Core
            // no consigue ejecutar el orphan-removal sobre items tracked y
            // termina lanzando una violación de FK al hacer SaveChanges.
            await _stageRepository.RemoveOptionsAsync(stage.Id, cancellationToken);
            stage.ReplaceOptions(request.Options.Select(o => (o.Text, o.IsCorrect)));
        }

        await _stageRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(true);
    }
}
