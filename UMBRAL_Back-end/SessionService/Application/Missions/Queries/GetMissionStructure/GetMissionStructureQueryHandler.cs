namespace SessionService.Application.Missions.Queries.GetMissionStructure;

using MediatR;
using SessionService.Application.Missions.Composite;
using SessionService.Domain.Common;
using SessionService.Domain.MissionLookup;

/// <summary>
/// Resuelve el preview de estructura de una misión: valida que la misión exista,
/// delega el ensamblado del árbol Composite (Mission → Stages → Clues) en
/// <see cref="IMissionStructureTreeBuilder"/>, lo recorre una vez con un
/// <see cref="MissionSummaryVisitor"/> para el resumen agregado y proyecta el DTO.
/// </summary>
public class GetMissionStructureQueryHandler
    : IRequestHandler<GetMissionStructureQuery, Result<MissionStructureDto>>
{
    private readonly IMissionLookupRepository _missionLookup;
    private readonly IMissionStructureTreeBuilder _treeBuilder;

    public GetMissionStructureQueryHandler(
        IMissionLookupRepository missionLookup,
        IMissionStructureTreeBuilder treeBuilder)
    {
        _missionLookup = missionLookup;
        _treeBuilder = treeBuilder;
    }

    public async Task<Result<MissionStructureDto>> Handle(
        GetMissionStructureQuery request, CancellationToken cancellationToken)
    {
        // 1. The mission must be known to this service (seeded via integration events).
        var lookup = await _missionLookup.GetByIdAsync(request.MissionId, cancellationToken);
        if (lookup is null)
            return Result.Failure<MissionStructureDto>(MissionStructureErrors.MissionNotFound);

        // 2. Assemble the Composite tree from StageService + ClueService (delegated).
        var mission = await _treeBuilder.BuildAsync(lookup, cancellationToken);

        // 3. One traversal computes the whole summary (Visitor pattern).
        var summaryVisitor = new MissionSummaryVisitor();
        mission.Accept(summaryVisitor);
        var summary = summaryVisitor.Build();

        // 4. Project the tree to a serializable DTO.
        var stageDtos = mission.Children
            .OfType<StageComponent>()
            .Select(s => new StageNodeDto(
                s.Id, s.Name, s.Type, s.Order, s.BaseScore,
                s.Children
                    .OfType<ClueComponent>()
                    .Select(c => new ClueNodeDto(c.Id, c.Order, c.Content))
                    .ToList()))
            .ToList();

        var dto = new MissionStructureDto(
            mission.Id,
            mission.Name,
            mission.Difficulty,
            mission.Status,
            new MissionStructureSummaryDto(
                summary.StageCount,
                summary.ClueCount,
                summary.TriviaStages,
                summary.TreasureHuntStages,
                summary.TotalScore,
                summary.StagesWithoutClues),
            stageDtos);

        return Result.Success(dto);
    }
}
