namespace SessionService.Application.Sessions.Commands.StartSession;

using MediatR;
using SessionService.Domain.Common;

public record StartSessionCommand(Guid SessionId) : IRequest<Result<bool>>;
