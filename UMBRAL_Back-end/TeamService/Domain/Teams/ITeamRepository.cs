namespace TeamService.Domain.Teams;

public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
