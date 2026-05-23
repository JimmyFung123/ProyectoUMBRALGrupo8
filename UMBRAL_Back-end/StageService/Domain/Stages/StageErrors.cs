namespace StageService.Domain.Stages;
using StageService.Domain.Common;
public static class StageErrors
{
    public static readonly Error NotFound = new("Stage.NotFound", "Stage not found.");
    public static readonly Error MissionNotFound = new("Stage.MissionNotFound", "Mission not found in lookup.");
    public static readonly Error MissionNotActive = new("Stage.MissionNotActive", "Mission is not active.");
    public static readonly Error MissionIsActive = new("Stage.MissionIsActive", "Cannot modify stages of an active mission.");
    public static readonly Error NotFound2 = new("Stage.StageNotFound", "Stage not found.");
    public static readonly Error InvalidName = new("Stage.InvalidName", "Stage name cannot be empty.");
}
