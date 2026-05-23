namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Domain.MissionLookup;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to MissionActivatedIntegrationEvent published by MissionService.
/// Updates the local MissionsLookup so SessionService allows new session creation.
/// </summary>
public class MissionActivatedConsumer : IConsumer<MissionActivatedIntegrationEvent>
{
    private readonly IMissionLookupRepository _repository;

    public MissionActivatedConsumer(IMissionLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<MissionActivatedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByIdAsync(context.Message.MissionId, context.CancellationToken);
        if (lookup is null) return;  // event arrived before MissionCreated — will be reconciled later

        lookup.UpdateStatus("Active");
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
