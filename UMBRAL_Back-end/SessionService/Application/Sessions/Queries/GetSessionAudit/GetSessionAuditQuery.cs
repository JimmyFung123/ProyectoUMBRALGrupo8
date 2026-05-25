namespace SessionService.Application.Sessions.Queries.GetSessionAudit;

using MediatR;
using SessionService.Domain.Common;

public record GetSessionAuditQuery(Guid SessionId) : IRequest<Result<SessionAuditDto>>;
