namespace SessionService.Application.Sessions.Commands.FinalizeSession;

using MediatR;
using SessionService.Domain.Common;

public record FinalizeSessionCommand(Guid SessionId, string? OperatorName = null) : IRequest<Result<bool>>;
