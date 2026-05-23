namespace UMBRAL_Back_end.Application.Missions.Commands.UpdateStage;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;

public class UpdateStageCommandHandler : IRequestHandler<UpdateStageCommand, Result>
{
    private readonly IMissionRepository _repository;

    public UpdateStageCommandHandler(IMissionRepository repository)
    {
        _repository = repository;
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

        // Replace Trivia options if provided
        if (stage.Type == StageType.Trivia && request.Options is not null)
        {
            var options = request.Options.Select(o => (o.Text, o.IsCorrect));
            await _repository.ReplaceStageOptionsAsync(request.StageId, options, cancellationToken);
        }

        await _repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
