namespace TeamService.Application.Teams.Commands.ReleaseClue;

using MediatR;
using TeamService.Domain.Common;

/// <summary>
/// Records the release of the next sequential clue to a team.
/// Returns the new CluesReceivedCurrentStage count.
/// </summary>
public record ReleaseClueCommand(Guid TeamId, int TotalCluesForStage, bool IsAutomatic = false)
    : IRequest<Result<int>>;
