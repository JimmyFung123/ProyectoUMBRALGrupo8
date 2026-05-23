namespace UMBRAL_Back_end.Application.Missions.Commands.RemoveStage;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class RemoveStageCommandHandler : IRequestHandler<RemoveStageCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;

    public RemoveStageCommandHandler(IMissionRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
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

        var stageId = stage.Id;

        // Explicitly mark the stage for deletion in EF Core
        await _repository.RemoveStageAsync(stage, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new MissionStageRemovedEvent(mission.Id, stageId, DateTime.UtcNow),
            cancellationToken);

        return Result.Success();
    }
}
