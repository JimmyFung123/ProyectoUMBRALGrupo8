namespace SessionService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;

/// <summary>
/// SessionService's own database — completely isolated from other services' DBs.
/// Contains: Sessions, SessionEvents, and MissionsLookup (read-side replica of missions data).
/// </summary>
public class SessionsDbContext : DbContext
{
    public SessionsDbContext(DbContextOptions<SessionsDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();
    public DbSet<MissionLookup> MissionsLookup => Set<MissionLookup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SessionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
