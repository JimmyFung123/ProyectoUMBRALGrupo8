namespace SessionService.Application.Missions.Queries.GetMissionStructure;

using SessionService.Application.Missions.Composite;
using SessionService.Application.Sessions;
using SessionService.Domain.MissionLookup;

/// <summary>
/// Ensambla el árbol Composite de una misión (Mission → Stages → Clues) a partir
/// de los datos que viven en otros servicios (StageService y ClueService).
///
/// SRP: se extrae de <see cref="GetMissionStructureQueryHandler"/> para que esa
/// responsabilidad —cruzar dos clientes HTTP y armar la estructura— tenga una sola
/// razón de cambio y se pueda probar en aislamiento. El handler queda solo con la
/// orquestación del query (validar, recorrer con el Visitor y proyectar el DTO).
/// </summary>
public interface IMissionStructureTreeBuilder
{
    /// <summary>
    /// Construye y devuelve la raíz del árbol (<see cref="MissionComponent"/>) con
    /// sus etapas y pistas ya colgadas y ordenadas.
    /// </summary>
    Task<MissionComponent> BuildAsync(MissionLookup lookup, CancellationToken ct);
}

public sealed class MissionStructureTreeBuilder : IMissionStructureTreeBuilder
{
    private readonly IStageServiceClient _stageClient;
    private readonly IClueServiceClient _clueClient;

    public MissionStructureTreeBuilder(IStageServiceClient stageClient, IClueServiceClient clueClient)
    {
        _stageClient = stageClient;
        _clueClient = clueClient;
    }

    public async Task<MissionComponent> BuildAsync(MissionLookup lookup, CancellationToken ct)
    {
        // Mission (root) → Stages → Clues.
        var mission = new MissionComponent(lookup.Id, lookup.Name, lookup.Difficulty, lookup.Status);

        var stages = await _stageClient.GetStagesByMissionAsync(lookup.Id, ct);
        foreach (var stageRef in stages.OrderBy(s => s.Order))
        {
            var detail = await _stageClient.GetStageWithOptionsAsync(stageRef.Id, ct);

            var stageComponent = detail is not null
                ? new StageComponent(detail.Id, detail.Title, detail.Type, detail.Order, detail.BaseScore)
                : new StageComponent(stageRef.Id, $"Etapa {stageRef.Order}", stageRef.Type, stageRef.Order, 0);

            var clues = await _clueClient.GetCluesByStageAsync(stageRef.Id, ct);
            foreach (var clue in clues.OrderBy(c => c.Order))
                stageComponent.Add(new ClueComponent(clue.Id, clue.Order, clue.Content));

            mission.Add(stageComponent);
        }

        return mission;
    }
}
