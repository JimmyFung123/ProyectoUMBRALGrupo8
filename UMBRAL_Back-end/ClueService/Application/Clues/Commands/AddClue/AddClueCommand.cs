namespace ClueService.Application.Clues.Commands.AddClue;
using MediatR;
using ClueService.Domain.Common;

public record AddClueCommand(
    Guid StageId,
    int Order,
    string? Content,
    double? Latitude,
    double? Longitude,
    int? RadiusMeters,
    int? AutoReleaseAfterMinutes = null
) : IRequest<Result<Guid>>;
