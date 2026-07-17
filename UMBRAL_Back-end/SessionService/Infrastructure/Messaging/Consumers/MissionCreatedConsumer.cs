namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Domain.MissionLookup;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to MissionCreatedIntegrationEvent published by MissionService.
/// Seeds the local MissionsLookup table with Status = Inactive.
/// </summary>
public class MissionCreatedConsumer : IConsumer<MissionCreatedIntegrationEvent>
{
    private readonly IMissionLookupRepository _repository;

    public MissionCreatedConsumer(IMissionLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<MissionCreatedIntegrationEvent> context)
    {
        var existing = await _repository.GetByIdAsync(context.Message.MissionId, context.CancellationToken);
        if (existing is not null) return;  // idempotent — skip if already seeded

        var lookup = MissionLookup.Create(
            context.Message.MissionId,
            context.Message.Name,
            context.Message.Status);

        await _repository.AddAsync(lookup, context.CancellationToken);
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
