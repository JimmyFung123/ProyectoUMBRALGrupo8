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
    private readonly IHubContext<SessionHub> _hub;

    public ReleaseClueCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamServiceClient,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _teamServiceClient = teamServiceClient;
        _hub = hub;
    }

    public async Task<Result<bool>> Handle(ReleaseClueCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        // Clues can only be released while the session is active
        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<bool>(SessionErrors.CannotReleaseClue);

        var cluesReceived = await _teamServiceClient.ReleaseClueAsync(
            request.TeamId,
            request.TotalCluesForStage,
            cancellationToken);

        if (cluesReceived < 0)
            return Result.Failure<bool>(SessionErrors.AllCluesAlreadyReleased);

        // Notify all connected participants in the session group
        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("ClueReleased",
                new
                {
                    SessionId  = request.SessionId,
                    TeamId     = request.TeamId,
                    ClueContent = request.ClueContent,
                    ClueNumber = cluesReceived,
                },
                cancellationToken);

        return Result.Success(true);
    }
}
