namespace SessionService.Application.Sessions.Queries.GetSessionByCode;
using MediatR;
using SessionService.Domain.Common;
public record GetSessionByCodeQuery(string Code) : IRequest<Result<SessionByCodeDto>>;
public record SessionByCodeDto(Guid Id, string Name, string Status, string AccessCode, Guid MissionId);
