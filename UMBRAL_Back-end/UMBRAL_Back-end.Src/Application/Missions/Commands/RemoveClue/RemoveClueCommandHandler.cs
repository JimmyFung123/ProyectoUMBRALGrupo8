namespace UMBRAL_Back_end.Application.Missions.Commands.RemoveClue;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class RemoveClueCommandHandler : IRequestHandler<RemoveClueCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;

    public RemoveClueCommandHandler(IMissionRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Result> Handle(RemoveClueCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        // Domain returns the removed clue so we can pass it to the repository
        var removeResult = mission.RemoveClue(request.StageId, request.ClueId, hasActiveSessions);
        if (removeResult.IsFailure)
            return Result.Failure(removeResult.Error);

        var removedClueId = removeResult.Value.Id;

        await _repository.RemoveClueAsync(removeResult.Value, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new TreasureClueRemovedEvent(mission.Id, request.StageId, removedClueId, DateTime.UtcNow),
            cancellationToken);

        return Result.Success();
    }
}
