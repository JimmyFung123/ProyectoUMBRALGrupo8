namespace TeamService.Domain.Teams;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<Team?> GetByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken = default);
    Task AddAsync(Team team, CancellationToken cancellationToken = default);
}
