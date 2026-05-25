namespace SessionService.Application.Sessions.Commands.ResumeSession;

using MediatR;
using SessionService.Domain.Common;

public record ResumeSessionCommand(Guid SessionId, string? OperatorName = null) : IRequest<Result<bool>>;
