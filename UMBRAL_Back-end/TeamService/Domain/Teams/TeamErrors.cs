namespace TeamService.Domain.Teams;

using TeamService.Domain.Common;

public static class TeamErrors
{
    public static readonly Error NotFound = new("Team.NotFound", "Team not found.");
}
