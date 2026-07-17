namespace SessionService.Application.Sessions.Commands.StartSession;

using MediatR;
using SessionService.Domain.Common;

public record StartSessionCommand(Guid SessionId, string? OperatorName = null) : IRequest<Result<bool>>;
