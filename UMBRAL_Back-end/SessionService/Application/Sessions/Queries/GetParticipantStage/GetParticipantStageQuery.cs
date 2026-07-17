namespace SessionService.Application.Sessions.Queries.GetParticipantStage;

using MediatR;
using SessionService.Domain.Common;

public record GetParticipantStageQuery(Guid SessionId, Guid TeamId) : IRequest<Result<ParticipantStageDto>>;
