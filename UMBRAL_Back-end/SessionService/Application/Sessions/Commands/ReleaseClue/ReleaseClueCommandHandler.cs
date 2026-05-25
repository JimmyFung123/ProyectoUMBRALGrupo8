namespace SessionService.Application.Sessions.Commands.ReleaseClue;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;

public class ReleaseClueCommandHandler : IRequestHandler<ReleaseClueCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamServiceClient;
    private readonly ISessionEventRepository _eventRepository;
    private readonly IHubContext<SessionHub> _hub;

    public ReleaseClueCommandHandler(
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

    public async Task<Result<bool>> Handle(ReleaseClueCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<bool>(SessionErrors.CannotReleaseClue);

        var cluesReceived = await _teamServiceClient.ReleaseClueAsync(
            request.TeamId,
            request.TotalCluesForStage,
            cancellationToken,
            isAutomatic: false);

        if (cluesReceived < 0)
            return Result.Failure<bool>(SessionErrors.AllCluesAlreadyReleased);

        // Audit log — resolve team name for human-readable message
        var teamInfo = await _teamServiceClient.GetTeamByIdAsync(request.TeamId, cancellationToken);
        var teamName = teamInfo?.Name ?? request.TeamId.ToString();

        var auditMessage = request.ClueContent is not null
            ? $"Pista #{cluesReceived} liberada al equipo '{teamName}': \"{request.ClueContent}\"."
            : $"Pista #{cluesReceived} liberada al equipo '{teamName}': zona geográfica (radio {request.ClueRadiusMeters ?? 0}m).";

        var auditEvent = SessionEvent.Create(request.SessionId, auditMessage);
        await _eventRepository.AddAsync(auditEvent, cancellationToken);
        await _eventRepository.SaveChangesAsync(cancellationToken);

        // Notify all connected participants in the session group
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("ClueReleased",
                new
                {
                    SessionId      = request.SessionId,
                    TeamId         = request.TeamId,
                    ClueContent    = request.ClueContent,
                    ClueLatitude   = request.ClueLatitude,
                    ClueLongitude  = request.ClueLongitude,
                    ClueRadiusMeters = request.ClueRadiusMeters,
                    ClueNumber     = cluesReceived,
                    IsAutomatic    = false,
                },
                cancellationToken);

        return Result.Success(true);
    }
}
