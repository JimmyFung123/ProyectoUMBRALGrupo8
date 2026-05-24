namespace SessionService.Application.Sessions.Commands.FinalizeSession;

using MediatR;
using SessionService.Domain.Common;

public record FinalizeSessionCommand(Guid SessionId) : IRequest<Result<bool>>;
