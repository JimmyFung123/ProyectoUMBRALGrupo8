namespace ClueService.Domain.StageLookup;
public class StageLookup
{
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private StageLookup() { }

    public static StageLookup Create(Guid stageId, Guid missionId, string name)
        => new() { Id = stageId, MissionId = missionId, Name = name, CreatedAt = DateTime.UtcNow };
}
