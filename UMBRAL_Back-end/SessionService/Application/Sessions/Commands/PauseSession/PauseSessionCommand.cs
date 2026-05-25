namespace SessionService.Application.Sessions.Commands.PauseSession;

using MediatR;
using SessionService.Domain.Common;

public record PauseSessionCommand(Guid SessionId, string? OperatorName = null) : IRequest<Result<bool>>;
