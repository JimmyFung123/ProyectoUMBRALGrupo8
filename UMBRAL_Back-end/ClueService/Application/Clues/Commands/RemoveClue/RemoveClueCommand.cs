namespace ClueService.Application.Clues.Commands.RemoveClue;
using MediatR;
using ClueService.Domain.Common;
public record RemoveClueCommand(Guid ClueId) : IRequest<Result<bool>>;
