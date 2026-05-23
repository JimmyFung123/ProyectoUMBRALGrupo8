namespace SessionService.Domain.MissionLookup;

/// <summary>
/// Read-side replica of Mission data owned by MissionService.
/// Populated and updated by consuming integration events from RabbitMQ.
/// SessionService NEVER writes to MissionService's database directly.
/// </summary>
public class MissionLookup
{
    public Guid Id { get; private set; }      // = MissionId from MissionService
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;  // "Active" | "Inactive"
    public DateTime LastUpdatedAt { get; private set; }

    private MissionLookup() { }

    public static MissionLookup Create(Guid missionId, string name, string status)
        => new()
        {
            Id = missionId,
            Name = name,
            Status = status,
            LastUpdatedAt = DateTime.UtcNow
        };

    public void UpdateStatus(string newStatus)
    {
        Status = newStatus;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public bool IsActive => Status == "Active";
}
