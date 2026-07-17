namespace StageService.Infrastructure.Messaging.Consumers;
using MassTransit;
using StageService.Domain.MissionLookup;
using UMBRAL.Contracts.Events;

public class MissionActivatedConsumer : IConsumer<MissionActivatedIntegrationEvent>
{
    private readonly IMissionLookupRepository _repository;
    public MissionActivatedConsumer(IMissionLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<MissionActivatedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByIdAsync(context.Message.MissionId, context.CancellationToken);
        if (lookup is null) return;
        lookup.UpdateStatus("Active");
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
