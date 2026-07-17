namespace SessionService.Infrastructure.Messaging.Consumers;

using MassTransit;
using SessionService.Domain.MissionLookup;
using UMBRAL.Contracts.Events;

/// <summary>
/// Reacts to MissionActivatedIntegrationEvent published by MissionService.
/// Updates the local MissionsLookup so SessionService allows new session creation.
/// Self-heals: if the MissionCreated event never arrived (e.g. the consumer was
/// offline when MissionService published it), the lookup row is created here
/// using the data carried by the Activated event itself.
/// </summary>
public class MissionActivatedConsumer : IConsumer<MissionActivatedIntegrationEvent>
{
    private readonly IMissionLookupRepository _repository;

    public MissionActivatedConsumer(IMissionLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<MissionActivatedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByIdAsync(context.Message.MissionId, context.CancellationToken);

        if (lookup is null)
        {
            var newLookup = MissionLookup.Create(
                context.Message.MissionId,
                context.Message.Name,
                "Active",
                context.Message.Difficulty);
            await _repository.AddAsync(newLookup, context.CancellationToken);
        }
        else
        {
            lookup.UpdateStatus("Active");
            lookup.UpdateDifficulty(context.Message.Difficulty);
        }

        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
