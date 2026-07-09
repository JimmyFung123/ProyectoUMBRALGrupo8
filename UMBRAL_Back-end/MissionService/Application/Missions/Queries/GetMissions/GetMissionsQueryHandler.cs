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
        MissionStatus? status = null;
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<MissionStatus>(request.Status, true, out var parsed))
            status = parsed;

        var missions = await _repository.GetAllAsync(status, cancellationToken);

        return missions
            .Select(m => new MissionDto(
                m.Id,
                m.Name,
                m.Description,
                m.Difficulty.ToString(),
                m.MaxDuration,
                m.Status.ToString(),
                m.CreatedAt,
                m.UpdatedAt))
            .ToList();
    }
}
