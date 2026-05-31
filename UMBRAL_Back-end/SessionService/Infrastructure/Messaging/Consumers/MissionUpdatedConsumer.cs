namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Domain.MissionLookup;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to MissionUpdatedIntegrationEvent published by MissionService.
/// Keeps the local MissionsLookup Difficulty field in sync so IScoringStrategy
/// can apply the correct score multiplier during active sessions.
/// </summary>
public class MissionUpdatedConsumer : IConsumer<MissionUpdatedIntegrationEvent>
{
    private readonly IMissionLookupRepository _repository;

    public MissionUpdatedConsumer(IMissionLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<MissionUpdatedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByIdAsync(context.Message.MissionId, context.CancellationToken);
        if (lookup is null) return;  // mission not yet in lookup — will be added on next Activate

        lookup.UpdateDifficulty(context.Message.Difficulty);
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
