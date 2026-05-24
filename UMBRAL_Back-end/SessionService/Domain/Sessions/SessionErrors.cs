namespace SessionService.Domain.Sessions;

using SessionService.Domain.Common;

public static class SessionErrors
{
    public static readonly Error NotFound                  = new("Session.NotFound", "Session not found.");
    public static readonly Error InvalidName               = new("Session.InvalidName", "Session name is required.");
    public static readonly Error MissionNotActive          = new("Session.MissionNotActive", "Cannot create a session for a mission that is not active.");
    public static readonly Error CannotEditNonPendingSession   = new("Session.CannotEdit",   "Only sessions in 'Pending' state can be edited.");
    public static readonly Error CannotCancelNonPendingSession = new("Session.CannotCancel", "Only sessions in 'Pending' state can be cancelled.");
    public static readonly Error CannotStartSession  = new("Session.CannotStart",  "Only sessions in 'Pending' state can be started.");
    public static readonly Error CannotPauseSession  = new("Session.CannotPause",  "Only sessions in 'InProgress' state can be paused.");
    public static readonly Error CannotResumeSession = new("Session.CannotResume", "Only sessions in 'Paused' state can be resumed.");
    public static readonly Error CannotFinalizeSession  = new("Session.CannotFinalize", "Only active or paused sessions can be finalized.");
    public static readonly Error NoTeamsEnrolled         = new("Session.NoTeamsEnrolled",     "The session cannot start because no teams are enrolled.");
    public static readonly Error CannotReleaseClue       = new("Session.CannotReleaseClue",    "Clues can only be released while the session is in progress.");
    public static readonly Error AllCluesAlreadyReleased = new("Session.AllCluesReleased",     "All configured clues for this stage have already been released to the team.");
    public static readonly Error CannotPenalizeTeam = new("Session.CannotPenalizeTeam", "Team penalties can only be applied while the session is in progress.");
    public static readonly Error CannotForceAdvance      = new("Session.CannotForceAdvance",      "Team advance can only be forced while the session is in progress.");
    public static readonly Error TeamAlreadyOnLastStage  = new("Session.TeamAlreadyOnLastStage",  "The team is already on the last stage and cannot be advanced further.");
    public static readonly Error TeamNotFound            = new("Session.TeamNotFound",            "The specified team is not enrolled in this session.");
}
