namespace SessionService.Application.Sessions.Commands.PenalizeTeam;

using MediatR;
using Microsoft.AspNetCore.SignalR;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Hubs;

public class PenalizeTeamCommandHandler : IRequestHandler<PenalizeTeamCommand, Result<int>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamClient;
    private readonly IHubContext<SessionHub> _hub;

    public PenalizeTeamCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamClient,
        IHubContext<SessionHub> hub)
    {
        _sessionRepository = sessionRepository;
        _teamClient = teamClient;
        _hub = hub;
    }

    public async Task<Result<int>> Handle(PenalizeTeamCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<int>(SessionErrors.NotFound);

        if (session.Status != SessionStatus.InProgress)
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        var newScore = await _teamClient.PenalizeTeamAsync(
            request.TeamId, request.Points, request.Reason, cancellationToken);

        if (newScore == int.MinValue)
            return Result.Failure<int>(SessionErrors.CannotPenalizeTeam);

        await _hub.Clients
            .Group(request.SessionId.ToString())
            .SendAsync("SessionStateChanged", cancellationToken);

        return Result.Success(newScore);
    }
}
