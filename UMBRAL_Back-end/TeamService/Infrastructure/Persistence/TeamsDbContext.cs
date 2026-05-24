namespace TeamService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using TeamService.Domain.Teams;

/// <summary>
/// TeamService's own isolated database (umbral_teams).
/// Completely decoupled from SessionService's umbral_sessions database.
/// </summary>
public class TeamsDbContext : DbContext
{
    public TeamsDbContext(DbContextOptions<TeamsDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeamsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
