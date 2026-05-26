namespace TeamService.Application.Rankings;

/// <summary>
/// Application service that keeps the CQRS ranking read model in sync with the
/// transactional <c>Teams</c> table (HU-24).
///
/// Called from command handlers AFTER they mutate the <c>Team</c> aggregate but
/// BEFORE the unit of work is committed — the projector and the write share a
/// single <c>SaveChanges</c> so the projection can never drift from the write
/// model within the same request.
///
/// The projector recomputes the entire session on every call (small N, ≤ ~50
/// teams) instead of patching individual rows: keeps the sort/rank invariants
/// trivially correct and avoids partial-update bugs.
/// </summary>
public interface IRankingProjector
{
    /// <summary>
    /// Recomputes the ranking for the given session from the current state of
    /// the <c>Team</c> aggregate and updates the projection rows in place
    /// (upsert + delete-stale).
    ///
    /// Sort contract (RB-08):
    ///   1. Score descending.
    ///   2. <c>LastStageCompletedAt</c> ascending (teams that haven't completed
    ///      any stage are pushed to the end).
    ///   3. Team name ascending (final stable tie-breaker).
    ///
    /// Dense ranking — tied rows share a <c>Rank</c>, the next distinct score
    /// gets the next integer. <c>Position</c> is always 1..N.
    /// </summary>
    Task RebuildAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>Deletes every projection row for the session (session cancelled).</summary>
    Task RemoveSessionAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
