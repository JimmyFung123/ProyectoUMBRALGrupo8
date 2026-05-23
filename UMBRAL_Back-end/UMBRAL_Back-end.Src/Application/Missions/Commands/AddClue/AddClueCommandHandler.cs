namespace UMBRAL_Back_end.Application.Missions.Commands.AddClue;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class AddClueCommandHandler : IRequestHandler<AddClueCommand, Result<Guid>>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;

    public AddClueCommandHandler(IMissionRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Result<Guid>> Handle(AddClueCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure<Guid>(MissionErrors.NotFound);

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var addResult = mission.AddClue(
            stageId: request.StageId,
            hasActiveSessions: hasActiveSessions,
            order: request.Order,
            content: request.Content,
            latitude: request.Latitude,
            longitude: request.Longitude,
            radiusMeters: request.RadiusMeters);

        if (addResult.IsFailure)
            return Result.Failure<Guid>(addResult.Error);

        var clue = addResult.Value;
        await _repository.AddClueAsync(clue, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new TreasureClueAddedEvent(mission.Id, request.StageId, clue.Id, clue.Order, DateTime.UtcNow),
            cancellationToken);

        return Result.Success(clue.Id);
    }
}
