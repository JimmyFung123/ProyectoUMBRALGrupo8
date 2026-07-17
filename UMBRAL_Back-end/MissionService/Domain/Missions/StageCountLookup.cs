namespace UMBRAL_Back_end.Domain.Missions;

public class StageCountLookup
{
    public Guid MissionId { get; private set; }
    public int Count { get; private set; }

    private StageCountLookup() { }

    public static StageCountLookup Create(Guid missionId) =>
        new() { MissionId = missionId, Count = 1 };

    public void Increment() => Count++;

    public void Decrement()
    {
        if (Count > 0) Count--;
    }

    public bool HasStages => Count > 0;
}
