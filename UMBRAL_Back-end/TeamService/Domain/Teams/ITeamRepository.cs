namespace TeamService.Domain.Teams;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
