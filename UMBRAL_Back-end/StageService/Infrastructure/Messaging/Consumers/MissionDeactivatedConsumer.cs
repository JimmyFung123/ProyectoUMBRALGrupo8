namespace StageService.Infrastructure.Messaging.Consumers;
using MassTransit;
using StageService.Domain.MissionLookup;
using UMBRAL.Contracts.Events;

public class MissionDeactivatedConsumer : IConsumer<MissionDeactivatedIntegrationEvent>
{
    private readonly IMissionLookupRepository _repository;
    public MissionDeactivatedConsumer(IMissionLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<MissionDeactivatedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByIdAsync(context.Message.MissionId, context.CancellationToken);
        if (lookup is null) return;
        lookup.UpdateStatus("Inactive");
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
