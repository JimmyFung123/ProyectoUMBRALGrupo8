namespace ClueService.Application.Clues.Commands.AddClue;
using MediatR;
using ClueService.Domain.Common;
public record AddClueCommand(Guid StageId, string Content) : IRequest<Result<Guid>>;
