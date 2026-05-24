namespace SessionService.Application.Sessions.Commands.PenalizeTeam;

using MediatR;
using SessionService.Domain.Common;

public record PenalizeTeamCommand(Guid SessionId, Guid TeamId, int Points, string Reason) : IRequest<Result<int>>;
