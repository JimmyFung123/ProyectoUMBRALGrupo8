namespace SessionService.Application.SyncHealth;

/// <summary>
/// HU-27 status thresholds for the sync-health dashboard.
///
/// Drift (write count != read count) is the only signal that actually proves
/// the projection is out of sync — it can only happen when an event was lost
/// or a consumer hasn't caught up yet. The wall-clock lag between writes and
/// the most recent projection update is purely informational: it grows when
/// no domain events are happening (idle system), which is normal. We surface
/// it on the card for transparency but it never alone marks a projection as
/// Critical, otherwise every quiet stretch lights the dashboard red.
///
/// Drift always wins. No drift, no problem.
/// </summary>
public static class SyncHealthClassifier
{
    public const string Healthy  = "Healthy";
    public const string Warning  = "Warning";
    public const string Critical = "Critical";

    /// <summary>
    /// Lag floor used by the front to colour the "Lag" metric cell. Same
    /// constant lives in <see cref="WarningLagSeconds"/> for legend display.
    /// </summary>
    public const int WarningLagSeconds  = 10;

    /// <summary>
    /// Kept for backwards compatibility with the legend, but no longer drives
    /// status classification on its own — see class-level remarks.
    /// </summary>
    public const int CriticalLagSeconds = 60;

    public static string Classify(int? lagSeconds, bool hasDrift)
    {
        // Drift is the single source of truth. Everything else is informational.
        return hasDrift ? Critical : Healthy;
    }

    public static int? ComputeLagSeconds(DateTime? lastUpdatedAt, DateTime now)
    {
        if (lastUpdatedAt is null) return null;
        var delta = (now - lastUpdatedAt.Value).TotalSeconds;
        return delta < 0 ? 0 : (int)Math.Floor(delta);
    }
}
