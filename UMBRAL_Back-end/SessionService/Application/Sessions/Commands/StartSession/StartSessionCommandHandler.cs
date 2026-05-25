namespace SessionService.Application.Sessions.Commands.StartSession;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;

public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamServiceClient;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IHubContext<SessionHub> _hub;

    public StartSessionCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamServiceClient,
        ISessionEventRepository eventRepository,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _teamServiceClient = teamServiceClient;
        _eventRepository = eventRepository;
        _hub = hub;
    }

    public async Task<Result<bool>> Handle(StartSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        // HU-12: at least one enrolled team is required to start
        var hasTeams = await _teamServiceClient.HasEnrolledTeamsAsync(request.SessionId, cancellationToken);
        if (!hasTeams)
            return Result.Failure<bool>(SessionErrors.NoTeamsEnrolled);

        var result = session.Start();
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-22: audit log of the state change
        var auditEvent = SessionEvent.Create(
            request.SessionId,
            "La sesión fue iniciada.",
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
