namespace TeamService.Infrastructure.Messaging.Consumers;

using MassTransit;
using UMBRAL.Contracts.Events;
using TeamService.Domain.Teams;

/// <summary>
/// Reacts to SessionCancelledIntegrationEvent published by SessionService.
/// Deletes all teams enrolled in the cancelled session from the TeamService database.
/// </summary>
public class SessionCancelledConsumer : IConsumer<SessionCancelledIntegrationEvent>
{
    private readonly ITeamRepository _teamRepository;

    public SessionCancelledConsumer(ITeamRepository teamRepository)
        => _teamRepository = teamRepository;

    public async Task Consume(ConsumeContext<SessionCancelledIntegrationEvent> context)
    {
        await _teamRepository.DeleteBySessionIdAsync(
            context.Message.SessionId,
            context.CancellationToken);

        await _teamRepository.SaveChangesAsync(context.CancellationToken);
    }
}
