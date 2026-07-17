namespace TeamService.Domain.Rankings;

/// <summary>
/// Persistence port for the CQRS ranking read model (HU-24).
///
/// The read side returns rows already ordered by <see cref="RankingProjection.Position"/>
/// — no sorting happens in the query handler. The write side is intentionally minimal
/// (upsert / delete by session) and only used by <c>IRankingProjector</c>.
/// </summary>
public interface IRankingProjectionRepository
{
    /// <summary>
    /// Returns every projection row for the session, ordered by <see cref="RankingProjection.Position"/>.
    /// Used by the query handler. No joins, no recomputation.
    /// </summary>
    Task<IReadOnlyList<RankingProjection>> GetBySessionIdOrderedAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a fresh projection row to the change tracker.</summary>
    Task AddAsync(RankingProjection projection, CancellationToken cancellationToken = default);

    /// <summary>Removes every projection row for the session (used on session cancellation).</summary>
    Task DeleteBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
