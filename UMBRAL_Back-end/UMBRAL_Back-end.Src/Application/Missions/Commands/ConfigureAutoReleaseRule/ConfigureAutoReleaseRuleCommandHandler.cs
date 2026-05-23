namespace UMBRAL_Back_end.Application.Missions.Commands.ConfigureAutoReleaseRule;

using MediatR;
using UMBRAL_Back_end.Domain.Common;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL_Back_end.Domain.Missions.Events;

public class ConfigureAutoReleaseRuleCommandHandler : IRequestHandler<ConfigureAutoReleaseRuleCommand, Result>
{
    private readonly IMissionRepository _repository;
    private readonly IPublisher _publisher;

    public ConfigureAutoReleaseRuleCommandHandler(IMissionRepository repository, IPublisher publisher)
    {
        _repository = repository;
        _publisher = publisher;
    }

    public async Task<Result> Handle(ConfigureAutoReleaseRuleCommand request, CancellationToken cancellationToken)
    {
        var mission = await _repository.GetByIdAsync(request.MissionId, cancellationToken);
        if (mission is null)
            return Result.Failure(MissionErrors.NotFound);

        bool hasActiveSessions = await _repository.HasActiveSessionsAsync(request.MissionId, cancellationToken);

        var result = mission.ConfigureAutoRelease(
            request.StageId,
            hasActiveSessions,
            request.TimeMinutes,
            request.MaxAttempts);

        if (result.IsFailure)
            return result;

        await _repository.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(
            new ClueReleaseRuleConfiguredEvent(
                mission.Id,
                request.StageId,
                request.TimeMinutes,
                request.MaxAttempts,
                DateTime.UtcNow),
            cancellationToken);

        return Result.Success();
    }
}
