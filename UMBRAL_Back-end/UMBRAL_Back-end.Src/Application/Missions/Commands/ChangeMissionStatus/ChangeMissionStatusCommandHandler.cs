namespace UMBRAL_Back_end.Application.Missions.Commands.ChangeMissionStatus;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class ChangeMissionStatusCommandHandler : IRequestHandler<ChangeMissionStatusCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;

    public ChangeMissionStatusCommandHandler(IMissionRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Result> Handle(ChangeMissionStatusCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var operationResult = request.Activate
            ? mission.Activate()
            : mission.Deactivate(hasActiveSessions);

        if (operationResult.IsFailure)
            return operationResult;

        await _repository.UpdateAsync(mission, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new MissionStatusChangedEvent(mission.Id, mission.Status.ToString(), mission.UpdatedAt!.Value),
            cancellationToken);

        return Result.Success();
    }
}
