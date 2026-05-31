namespace SessionService.Application.Missions.Queries.GetMissionStructure;

using MediatR;
using SessionService.Application.Missions.Composite;
using SessionService.Application.Sessions;
using SessionService.Domain.Common;
using SessionService.Domain.MissionLookup;

/// <summary>
/// Assembles a mission's full structure into a Composite tree
/// (<see cref="MissionComponent"/> → <see cref="StageComponent"/> →
/// <see cref="ClueComponent"/>) by pulling stage data from StageService and
/// clue data from ClueService, then walks it once with a
/// <see cref="MissionSummaryVisitor"/> to compute the aggregated summary.
/// </summary>
public class GetMissionStructureQueryHandler
    : IRequestHandler<GetMissionStructureQuery, Result<MissionStructureDto>>
{
    private readonly IMissionLookupRepository _missionLookup;
    private readonly IStageServiceClient _stageClient;
    private readonly IClueServiceClient _clueClient;

    public GetMissionStructureQueryHandler(
        IMissionLookupRepository missionLookup,
        IStageServiceClient stageClient,
        IClueServiceClient clueClient)
    {
        _missionLookup = missionLookup;
        _stageClient = stageClient;
        _clueClient = clueClient;
    }

    public async Task<Result<MissionStructureDto>> Handle(
        GetMissionStructureQuery request, CancellationToken cancellationToken)
    {
        // 1. The mission must be known to this service (seeded via integration events).
        var lookup = await _missionLookup.GetByIdAsync(request.MissionId, cancellationToken);
        if (lookup is null)
            return Result.Failure<MissionStructureDto>(MissionStructureErrors.MissionNotFound);

        // 2. Build the Composite tree: Mission (root) → Stages → Clues.
        var mission = new MissionComponent(lookup.Id, lookup.Name, lookup.Difficulty, lookup.Status);

        var stages = await _stageClient.GetStagesByMissionAsync(request.MissionId, cancellationToken);
        foreach (var stageRef in stages.OrderBy(s => s.Order))
        {
            var detail = await _stageClient.GetStageWithOptionsAsync(stageRef.Id, cancellationToken);

            var stageComponent = detail is not null
                ? new StageComponent(detail.Id, detail.Title, detail.Type, detail.Order, detail.BaseScore)
                : new StageComponent(stageRef.Id, $"Etapa {stageRef.Order}", stageRef.Type, stageRef.Order, 0);

            var clues = await _clueClient.GetCluesByStageAsync(stageRef.Id, cancellationToken);
            foreach (var clue in clues.OrderBy(c => c.Order))
                stageComponent.Add(new ClueComponent(clue.Id, clue.Order, clue.Content));

            mission.Add(stageComponent);
        }

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
