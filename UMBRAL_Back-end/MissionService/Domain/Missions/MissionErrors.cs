namespace UMBRAL_Back_end.Domain.Missions;

using UMBRAL_Back_end.Domain.Common;

public static class MissionErrors
{
    public static readonly Error NotFound =
        new("Mission.NotFound", "Mission not found.");

    public static readonly Error DuplicateName =
        new("Mission.DuplicateName", "A mission with this name already exists.");

    public static readonly Error HasActiveSessions =
        new("Mission.HasActiveSessions", "Cannot modify or disable a mission that has active sessions.");

    public static readonly Error InvalidName =
        new("Mission.InvalidName", "Mission name cannot be empty.");

    public static readonly Error InvalidDescription =
        new("Mission.InvalidDescription", "Mission description is too long.");

    public static readonly Error InvalidMaxDuration =
        new("Mission.InvalidMaxDuration", "Max duration must be greater than zero.");

    public static readonly Error NoStages =
        new("Mission.NoStages", "Mission must have at least one stage to be activated.");
}
