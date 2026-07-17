namespace ClueService.Infrastructure.Messaging.Consumers;
using MassTransit;
using ClueService.Domain.StageLookup;
using UMBRAL.Contracts.Events;

public class StageRemovedConsumer : IConsumer<StageRemovedIntegrationEvent>
{
    private readonly IStageLookupRepository _repository;
    public StageRemovedConsumer(IStageLookupRepository repository) => _repository = repository;

    public async Task Consume(ConsumeContext<StageRemovedIntegrationEvent> context)
    {
        var lookup = await _repository.GetByIdAsync(context.Message.StageId, context.CancellationToken);
        if (lookup is null) return;
        await _repository.DeleteAsync(lookup, context.CancellationToken);
        await _repository.SaveChangesAsync(context.CancellationToken);
    }
}
