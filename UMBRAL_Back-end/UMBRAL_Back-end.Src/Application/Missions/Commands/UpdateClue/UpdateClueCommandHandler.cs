namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateClue;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class UpdateClueCommandHandler : IRequestHandler<UpdateClueCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;

    public UpdateClueCommandHandler(IMissionRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Result> Handle(UpdateClueCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var updateResult = mission.UpdateClue(
            stageId: request.StageId,
            clueId: request.ClueId,
            hasActiveSessions: hasActiveSessions,
            order: request.Order,
            content: request.Content,
            latitude: request.Latitude,
            longitude: request.Longitude,
            radiusMeters: request.RadiusMeters);

        if (updateResult.IsFailure)
            return Result.Failure(updateResult.Error);

        await _repository.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new TreasureClueReorderedEvent(mission.Id, request.StageId, request.ClueId, request.Order, DateTime.UtcNow),
            cancellationToken);

        return Result.Success();
    }
}
