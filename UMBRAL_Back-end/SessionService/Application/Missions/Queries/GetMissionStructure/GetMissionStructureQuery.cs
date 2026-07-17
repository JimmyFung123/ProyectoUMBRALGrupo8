namespace SessionService.Application.Missions.Queries.GetMissionStructure;

using MediatR;
using SessionService.Domain.Common;

/// <summary>
/// Operator-facing preview: returns the full structure of a mission
/// (stages → clues) assembled into a Composite tree, plus an aggregated summary.
/// </summary>
public record GetMissionStructureQuery(Guid MissionId) : IRequest<Result<MissionStructureDto>>;
