namespace SessionService.Application.Sessions.Commands.LeaveTeam;

using MediatR;
using SessionService.Domain.Common;

/// <summary>
/// Participant action: leaves their team. Forwards to TeamService via
/// <see cref="ITeamServiceClient"/> — participants never call TeamService directly.
/// Not an operator action, so it does not write a <c>SessionEvent</c> audit entry.
/// </summary>
public record LeaveTeamCommand(Guid SessionId, Guid TeamId) : IRequest<Result>;
