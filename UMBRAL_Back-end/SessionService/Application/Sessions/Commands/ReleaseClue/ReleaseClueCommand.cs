namespace SessionService.Application.Sessions.Commands.ReleaseClue;

using MediatR;
using SessionService.Domain.Common;

/// <summary>
/// Releases the next clue in sequence to a specific team.
/// Validates the session is active, calls TeamService to record the release,
/// and broadcasts a SignalR notification to participants.
/// </summary>
public record ReleaseClueCommand(
    Guid SessionId,
    Guid TeamId,
    int TotalCluesForStage,
    string ClueContent
) : IRequest<Result<bool>>;
