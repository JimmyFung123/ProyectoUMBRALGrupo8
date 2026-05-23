namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateStage;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class UpdateStageCommandHandler : IRequestHandler<UpdateStageCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;

    public UpdateStageCommandHandler(IMissionRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Result> Handle(UpdateStageCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        // Check QR uniqueness (TreasureHunt only), excluding the current stage
        var stage = mission.Stages.FirstOrDefault(s => s.Id == request.StageId);
        if (stage is null)
            return Result.Failure(StageErrors.NotFound);

        if (stage.Type == StageType.TreasureHunt && !string.IsNullOrWhiteSpace(request.QrCode))
        {
            bool qrExists = await _repository.HasDuplicateQrCodeAsync(
                request.QrCode, excludeStageId: request.StageId, cancellationToken: cancellationToken);
            if (qrExists)
                return Result.Failure(StageErrors.DuplicateQrCode);
        }

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var updateResult = mission.UpdateStage(
            request.StageId,
            hasActiveSessions,
            request.Title,
            request.Order,
            request.BaseScore,
            request.Question,
            request.Latitude,
            request.Longitude,
            request.QrCode);

        if (updateResult.IsFailure)
            return Result.Failure(updateResult.Error);

        var updatedStage = updateResult.Value;

        // Replace Trivia options if provided
        if (stage.Type == StageType.Trivia && request.Options is not null)
        {
            var options = request.Options.Select(o => (o.Text, o.IsCorrect));
            await _repository.ReplaceStageOptionsAsync(request.StageId, options, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);

        if (updatedStage.Type == StageType.Trivia)
            await _publisher.Publish(
                new TriviaStageConfigure(mission.Id, updatedStage.Id, updatedStage.Question ?? "", updatedStage.Options.Count, DateTime.UtcNow),
                cancellationToken);
        else if (updatedStage.Type == StageType.TreasureHunt)
            await _publisher.Publish(
                new TreasureStageConfigure(mission.Id, updatedStage.Id, updatedStage.Latitude!.Value, updatedStage.Longitude!.Value, DateTime.UtcNow),
                cancellationToken);

        return Result.Success();
    }
}
