namespace SessionService.Application.Sessions.Commands.ResumeSession;

using MediatR;
using SessionService.Domain.Common;

public record ResumeSessionCommand(Guid SessionId) : IRequest<Result<bool>>;
