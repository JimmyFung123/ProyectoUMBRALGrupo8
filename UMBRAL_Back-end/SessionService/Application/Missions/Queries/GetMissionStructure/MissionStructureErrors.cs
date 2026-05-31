namespace SessionService.Application.Missions.Queries.GetMissionStructure;

using SessionService.Domain.Common;

public static class MissionStructureErrors
{
    public static readonly Error MissionNotFound = new(
        "MissionStructure.MissionNotFound",
        "No existe una misión con ese identificador en este servicio.");
}
