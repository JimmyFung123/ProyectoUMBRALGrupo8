namespace TeamService.Application.Teams.Commands.RecordWrongAttempt;

using MediatR;
using TeamService.Domain.Common;
using TeamService.Domain.Teams;

public record RecordWrongAttemptCommand(
    Guid TeamId,
    Guid BlockedOptionId,
    int ScorePenalty) : IRequest<Result<WrongAttemptOutcome>>;
