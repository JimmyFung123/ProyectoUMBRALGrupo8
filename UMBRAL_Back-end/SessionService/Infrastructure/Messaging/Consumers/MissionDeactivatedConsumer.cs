namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Domain.MissionLookup;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to MissionDeactivatedIntegrationEvent published by MissionService.
/// Updates the local MissionsLookup so SessionService blocks new session creation.
/// Self-heals: if the MissionCreated event never arrived, the lookup row is
/// created here using the data carried by the Deactivated event itself.
/// </summary>
public class MissionDeactivatedConsumer : IConsumer<MissionDeactivatedIntegrationEvent>
{
    private readonly IMissionLookupRepository _repository;

    public MissionDeactivatedConsumer(IMissionLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<MissionDeactivatedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByIdAsync(context.Message.MissionId, context.CancellationToken);

        if (lookup is null)
        {
            var newLookup = MissionLookup.Create(
                context.Message.MissionId,
                context.Message.Name,
                "Inactive");
            await _repository.AddAsync(newLookup, context.CancellationToken);
        }
        else
        {
            lookup.UpdateStatus("Inactive");
        }

        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
