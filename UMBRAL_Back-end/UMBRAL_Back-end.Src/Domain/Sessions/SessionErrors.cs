namespace UMBRAL_Back_end.Domain.Sessions;

using UMBRAL_Back_end.Domain.Common;

public static class SessionErrors
{
    public static readonly Error NotFound =
        new("Session.NotFound", "Session not found.");

    public static readonly Error InvalidName =
        new("Session.InvalidName", "Session name is required.");

    public static readonly Error MissionRequired =
        new("Session.MissionRequired", "A valid mission must be specified.");

    public static readonly Error MissionNotActive =
        new("Session.MissionNotActive", "Cannot create a session for a mission that is not active.");
}
