namespace SessionService.Application.Sessions.Commands.FinalizeSession;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;

public class FinalizeSessionCommandHandler : IRequestHandler<FinalizeSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IHubContext<SessionHub> _hub;

    public FinalizeSessionCommandHandler(
        ISessionRepository sessionRepository,
        ISessionEventRepository eventRepository,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _eventRepository = eventRepository;
        _hub = hub;
    }

    public async Task<Result<bool>> Handle(FinalizeSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        var result = session.Finalize();
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-22: audit log (irreversible state — last entry on the timeline)
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            "La sesión fue finalizada. Ranking definitivo calculado.",
            actorName: request.OperatorName);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("SessionStateChanged",
                new { SessionId = session.Id, NewStatus = session.Status.ToString() },
                cancellationToken);

        return Result.Success(true);
    }
}
