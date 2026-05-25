namespace SessionService.Application.Sessions.Commands.CancelSession;

using MediatR;
using SessionService.Domain.Common;

public record CancelSessionCommand(Guid SessionId, string? OperatorName = null) : IRequest<Result<bool>>;
