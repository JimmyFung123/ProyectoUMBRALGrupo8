namespace SessionService.Application.Sessions.Commands.SubmitTriviaAnswer;

using MediatR;
using SessionService.Domain.Common;

public record SubmitTriviaAnswerCommand(
    Guid SessionId,
    Guid TeamId,
    Guid StageId,
    Guid OptionId) : IRequest<Result<TriviaAnswerResultDto>>;
