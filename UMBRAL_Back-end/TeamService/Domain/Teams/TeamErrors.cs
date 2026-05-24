namespace TeamService.Domain.Teams;

using TeamService.Domain.Common;

public static class TeamErrors
{
    public static readonly Error NotFound             = new("Team.NotFound",             "Team not found.");
    public static readonly Error AllCluesReleased     = new("Team.AllCluesReleased",     "All clues for this stage have already been released to this team.");
}
