namespace StageService.Domain.MissionLookup;
public class MissionLookup
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public DateTime LastUpdatedAt { get; private set; }

    private MissionLookup() { }

    public static MissionLookup Create(Guid missionId, string name, string status)
        => new() { Id = missionId, Name = name, Status = status, LastUpdatedAt = DateTime.UtcNow };

    public void UpdateStatus(string newStatus) { Status = newStatus; LastUpdatedAt = DateTime.UtcNow; }
    public bool IsActive => Status == "Active";
}
