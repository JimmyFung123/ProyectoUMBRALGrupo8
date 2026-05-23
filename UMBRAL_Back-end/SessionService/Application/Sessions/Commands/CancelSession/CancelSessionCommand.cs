namespace SessionService.Application.Sessions.Commands.CancelSession;

using MediatR;
using SessionService.Domain.Common;

public record CancelSessionCommand(Guid SessionId) : IRequest<Result<bool>>;
