namespace ClueService.Infrastructure.Messaging.Consumers;
using MassTransit;
using ClueService.Domain.StageLookup;
using UMBRAL.Contracts.Events;

public class StageAddedConsumer : IConsumer<StageAddedIntegrationEvent>
{
    private readonly IStageLookupRepository _repository;
    public StageAddedConsumer(IStageLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<StageAddedIntegrationEvent> context)
    {
        var existing = await _repository.GetByIdAsync(context.Message.StageId, context.CancellationToken);
        if (existing is not null) return;
        var lookup = StageLookup.Create(context.Message.StageId, context.Message.MissionId, context.Message.StageType);
        await _repository.AddAsync(lookup, context.CancellationToken);
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
