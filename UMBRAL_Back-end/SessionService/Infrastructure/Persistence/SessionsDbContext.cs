namespace SessionService.Infrastructure.Persistence;

using MediatR;
using Microsoft.EntityFrameworkCore;
using SessionService.Domain.Common;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;
using SessionService.Domain.Statistics;

/// <summary>
/// SessionService's own database — completely isolated from other services' DBs.
/// Contains: Sessions, SessionEvents, MissionsLookup (read-side replica) and
/// StageCompletionRecords (HU-25 analytics fact table).
/// </summary>
public class SessionsDbContext : DbContext
{
    private readonly IMediator _mediator;

    public SessionsDbContext(DbContextOptions<SessionsDbContext> options, IMediator mediator) : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();
    public DbSet<MissionLookup> MissionsLookup => Set<MissionLookup>();

    /// <summary>HU-25 read model. Populated by gameplay handlers, flipped to
    /// visible by FinalizeSession.</summary>
    public DbSet<StageCompletionRecord> StageCompletionRecords => Set<StageCompletionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SessionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>Dispatches domain events raised on tracked aggregates after a
    /// successful commit (mirrors how integration events are already published
    /// post-save elsewhere in the codebase).</summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregatesWithEvents = ChangeTracker.Entries<AggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var aggregate in aggregatesWithEvents)
        {
            var domainEvents = aggregate.DomainEvents.ToList();
            aggregate.ClearDomainEvents();
            foreach (var domainEvent in domainEvents)
                await _mediator.Publish(domainEvent, cancellationToken);
        }

        return result;
    }
}
