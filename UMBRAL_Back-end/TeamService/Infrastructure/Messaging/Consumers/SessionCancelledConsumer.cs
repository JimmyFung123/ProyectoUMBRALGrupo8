namespace TeamService.Infrastructure.Messaging.Consumers;

using MassTransit;
using UMBRAL.Contracts.Events;
using TeamService.Application.Rankings;
using TeamService.Domain.Teams;

/// <summary>
/// Reacts to SessionCancelledIntegrationEvent published by SessionService.
/// Deletes all teams enrolled in the cancelled session from the TeamService
/// database AND drops the matching rows from the ranking projection (HU-24)
/// so the read model stays consistent with the write side.
/// </summary>
public class SessionCancelledConsumer : IConsumer<SessionCancelledIntegrationEvent>
{
    private readonly ITeamRepository _teamRepository;
    private readonly IRankingProjector _rankingProjector;

    public SessionCancelledConsumer(
        ITeamRepository teamRepository,
        IRankingProjector rankingProjector)
    {
        _teamRepository = teamRepository;
        _rankingProjector = rankingProjector;
    }

    public async Task Consume(ConsumeContext<SessionCancelledIntegrationEvent> context)
    {
        await _teamRepository.DeleteBySessionIdAsync(
            context.Message.SessionId,
            context.CancellationToken);

        await _rankingProjector.RemoveSessionAsync(
            context.Message.SessionId,
            context.CancellationToken);

        await _teamRepository.SaveChangesAsync(context.CancellationToken);
    }
}
