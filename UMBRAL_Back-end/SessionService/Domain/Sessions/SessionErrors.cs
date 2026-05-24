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
    public static readonly Error CannotFinalizeSession = new("Session.CannotFinalize", "Only active or paused sessions can be finalized.");
}
