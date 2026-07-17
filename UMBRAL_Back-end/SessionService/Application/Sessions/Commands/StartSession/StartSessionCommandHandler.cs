namespace SessionService.Application.Sessions.Commands.StartSession;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;

public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamServiceClient;

    public StartSessionCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamServiceClient)
    {
        _sessionRepository = sessionRepository;
        _teamServiceClient = teamServiceClient;
    }

    public async Task<Result<bool>> Handle(StartSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
            return Result.Failure<bool>(SessionErrors.NotFound);

        // HU-12 / RB-02: at least one enrolled team is required to start
        var hasTeams = await _teamServiceClient.HasEnrolledTeamsAsync(request.SessionId, cancellationToken);
        if (!hasTeams)
            return Result.Failure<bool>(SessionErrors.NoTeamsEnrolled);

        // RB-18: every team must have at least the minimum number of members (SessionStartPolicy)
        var allMeetMinimum = await _teamServiceClient.AllTeamsMeetMinimumMembersAsync(
            request.SessionId, minMembers: SessionStartPolicy.MinimumMembersPerTeam, cancellationToken);
        if (!allMeetMinimum)
            return Result.Failure<bool>(SessionErrors.TeamBelowMinimumMembers);

        var result = session.Start(request.OperatorName);
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(true);
    }
}
