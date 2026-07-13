namespace StageService.Domain.Stages;

using StageService.Domain.MissionLookup;

/// <summary>
/// Domain Service: "¿se puede modificar la estructura de una etapa ahora?".
/// Antes era `mission?.IsActive == true` (AddStage, RemoveStage) o
/// `mission is not null && mission.IsActive` (UpdateStage) — misma regla,
/// escrita distinto en cada handler. Si el lookup local todavía no existe
/// (evento MissionCreated/Activated aún no procesado), la misión se asume
/// Inactiva — self-heals cuando el evento llega.
/// </summary>
public static class StageMissionActivePolicy
{
    public static bool BlocksStageMutation(MissionLookup? mission) => mission?.IsActive == true;
}
