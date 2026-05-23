namespace UMBRAL_Back_end.Domain.Missions;

public class MissionStage
{
    public Guid Id { get; private set; }
    public Guid MissionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int Order { get; private set; }

    private MissionStage() { }

    public static MissionStage Create(Guid missionId, string name, int order) => new()
    {
        Id = Guid.NewGuid(),
        MissionId = missionId,
        Name = name,
        Order = order
    };
}
