namespace SessionService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
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
    public SessionsDbContext(DbContextOptions<SessionsDbContext> options) : base(options) { }

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
}
