namespace UMBRAL_Back_end.Infrastructure.Messaging.Consumers;

using MassTransit;
using UMBRAL_Back_end.Domain.Missions;
using UMBRAL.Contracts.Events;

public class StageAddedConsumer : IConsumer<StageAddedIntegrationEvent>
{
    private readonly IStageCountLookupRepository _repository;
    public StageAddedConsumer(IStageCountLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<StageAddedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByMissionIdAsync(context.Message.MissionId, context.CancellationToken);
        if (lookup is null)
        {
            lookup = StageCountLookup.Create(context.Message.MissionId);
            await _repository.AddAsync(lookup, context.CancellationToken);
        }
        else
        {
            lookup.Increment();
        }
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
