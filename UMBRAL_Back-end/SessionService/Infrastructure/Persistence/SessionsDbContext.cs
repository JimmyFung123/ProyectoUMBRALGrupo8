namespace SessionService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SessionService.Domain.MissionLookup;
using SessionService.Domain.Sessions;

/// <summary>
/// SessionService's own database — completely isolated from MissionService's DB.
/// Contains: Sessions, Teams, and MissionsLookup (read-side replica of missions data).
/// </summary>
public class SessionsDbContext : DbContext
{
    public SessionsDbContext(DbContextOptions<SessionsDbContext> options) : base(options) { }

    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<MissionLookup> MissionsLookup => Set<MissionLookup>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SessionsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
