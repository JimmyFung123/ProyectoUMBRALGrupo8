namespace ClueService.Application.Clues.Commands.UpdateClue;
using MediatR;
using ClueService.Domain.Common;

public record UpdateClueCommand(
    Guid ClueId,
    int Order,
    string? Content,
    double? Latitude,
    double? Longitude,
    int? RadiusMeters,
    int? AutoReleaseAfterMinutes
) : IRequest<Result<bool>>;
