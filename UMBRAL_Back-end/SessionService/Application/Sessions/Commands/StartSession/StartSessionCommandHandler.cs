namespace SessionService.Application.Sessions.Commands.StartSession;

using MediatR;
using SessionService.Application;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Contracts.Events;

public class StartSessionCommandHandler : IRequestHandler<StartSessionCommand, Result<bool>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ITeamServiceClient _teamServiceClient;
    private readonly IIntegrationEventBus _bus;

    public StartSessionCommandHandler(
        ISessionRepository sessionRepository,
        ITeamServiceClient teamServiceClient,
        IIntegrationEventBus bus)
    {
        _sessionRepository = sessionRepository;
        _teamServiceClient = teamServiceClient;
        _bus = bus;
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

        var result = session.Start();
        if (result.IsFailure)
            return result;

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        // HU-22 / HU-26: audit log of the state change
        await _bus.PublishAsync(
            new SessionAuditIntegrationEvent(
                request.SessionId,
                "La sesión fue iniciada.",
                ActorName: request.OperatorName,
                CommandType: nameof(StartSessionCommand),
                Outcome: SessionEvent.OutcomeSuccess,
                DateTime.UtcNow),
            cancellationToken);

        await _bus.PublishAsync(
            new SessionStateChangedIntegrationEvent(session.Id, session.Status.ToString()),
            cancellationToken);

        return Result.Success(true);
    }
}
