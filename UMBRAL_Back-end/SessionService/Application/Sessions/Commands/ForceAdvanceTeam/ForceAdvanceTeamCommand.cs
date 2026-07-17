namespace SessionService.Application.Sessions.Commands.ForceAdvanceTeam;

using MediatR;
using SessionService.Domain.Common;

public record ForceAdvanceTeamCommand(
    Guid SessionId,
    Guid TeamId,
    string? OperatorName = null) : IRequest<Result<bool>>;
