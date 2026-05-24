namespace TeamService.Application.Teams.Commands.AnswerTrivia;

using MediatR;
using TeamService.Domain.Common;

public record AnswerTriviaCommand(
    Guid TeamId,
    bool IsCorrect,
    int ScoreChange,
    int NextStageOrder) : IRequest<Result<int>>;
