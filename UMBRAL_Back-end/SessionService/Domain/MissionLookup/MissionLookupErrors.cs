namespace SessionService.Domain.MissionLookup;

using SessionService.Domain.Common;

public static class MissionLookupErrors
{
    public static readonly Error NotFound =
        new("MissionLookup.NotFound",
            "Mission not found in this service's lookup table. " +
            "The mission may not exist or its creation event has not arrived yet.");
}
