namespace UMBRAL_Back_end.Application.Missions.Queries.GetMissions;

using MediatR;
using UMBRAL_Back_end.Domain.Missions;

public class GetMissionsQueryHandler : IRequestHandler<GetMissionsQuery, IReadOnlyList<MissionDto>>
{
    private readonly IMissionRepository _repository;

    public GetMissionsQueryHandler(IMissionRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<MissionDto>> Handle(GetMissionsQuery request, CancellationToken cancellationToken)
    {
        var missions = await _repository.GetAllAsync(cancellationToken);

        return missions
            .Select(m => new MissionDto(
                m.Id,
                m.Name,
                m.Description,
                m.Difficulty.ToString(),
                m.MaxDuration,
                m.Status.ToString(),
                m.Stages.Count,
                m.CreatedAt,
                m.UpdatedAt))
            .ToList();
    }
}
