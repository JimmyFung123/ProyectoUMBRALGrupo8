namespace TeamService.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using TeamService.Domain.Rankings;
using TeamService.Domain.Teams;

/// <summary>
/// TeamService's own isolated database (umbral_teams).
/// Completely decoupled from SessionService's umbral_sessions database.
///
/// Holds both the transactional write model (<see cref="Teams"/>) and the
/// CQRS read-model projection (<see cref="RankingProjections"/>, HU-24). The
/// two tables coexist in the same physical DB but are touched by different
/// code paths: writes go through the Team aggregate, reads of the ranking
/// only hit the pre-calculated projection.
/// </summary>
public class TeamsDbContext : DbContext
{
    public TeamsDbContext(DbContextOptions<TeamsDbContext> options) : base(options) { }

    public DbSet<Team> Teams => Set<Team>();

    /// <summary>HU-24 read model. Populated by <c>IRankingProjector</c>.</summary>
    public DbSet<RankingProjection> RankingProjections => Set<RankingProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TeamsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
