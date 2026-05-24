namespace TeamService.Domain.Teams;

using TeamService.Domain.Common;

public static class TeamErrors
{
    public static readonly Error NotFound             = new("Team.NotFound",             "Team not found.");
    public static readonly Error AllCluesReleased     = new("Team.AllCluesReleased",     "All clues for this stage have already been released to this team.");
    public static readonly Error InvalidPenaltyPoints  = new("Team.InvalidPenaltyPoints",  "Penalty points must be greater than zero.");
    public static readonly Error PenaltyReasonRequired = new("Team.PenaltyReasonRequired", "A reason is required to apply a penalty.");
    public static readonly Error InvalidNextStage = new("Team.InvalidNextStage", "The next stage order must be greater than the current stage order.");
}
