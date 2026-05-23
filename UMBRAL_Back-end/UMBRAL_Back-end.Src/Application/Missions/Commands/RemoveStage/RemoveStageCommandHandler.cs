namespace UMBRAL_Back_end.Application.Missions.Commands.RemoveStage;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class RemoveStageCommandHandler : IRequestHandler<RemoveStageCommand, Result>
{
    private readonly IMissionRepository _repository;

    public RemoveStageCommandHandler(IMissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(RemoveStageCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        // Keep reference before removing from aggregate
        var stage = mission.Stages.FirstOrDefault(s => s.Id == request.StageId);
        if (stage is null)
            return Result.Failure(StageErrors.NotFound);

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var removeResult = mission.RemoveStage(request.StageId, hasActiveSessions);
        if (removeResult.IsFailure)
            return removeResult;

        // Explicitly mark the stage for deletion in EF Core
        await _repository.RemoveStageAsync(stage, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
