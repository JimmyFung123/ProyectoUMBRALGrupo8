namespace UMBRAL_Back_end.Infrastructure.Messaging.Consumers;

using MassTransit;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL.Contracts.Events;

public class StageRemovedConsumer : IConsumer<StageRemovedIntegrationEvent>
{
    private readonly IStageCountLookupRepository _repository;
    public StageRemovedConsumer(IStageCountLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<StageRemovedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByMissionIdAsync(context.Message.MissionId, context.CancellationToken);
        if (lookup is null) return;
        lookup.Decrement();
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
