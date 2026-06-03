namespace UMBRAL_Back_end.Adapter.Controllers;

public record CreateMissionRequest(string Name, string Description, string Difficulty, int MaxDuration);
public record UpdateMissionRequest(string Name, string Description, string Difficulty, int MaxDuration);
public record ChangeStatusRequest(bool Activate);
