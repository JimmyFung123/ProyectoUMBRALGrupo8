namespace StageService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;

public class StagesDbContext : DbContext
{
    public StagesDbContext(DbContextOptions<StagesDbContext> options) : base(options) { }
    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<TriviaOption> TriviaOptions => Set<TriviaOption>();
    public DbSet<MissionLookup> MissionsLookup => Set<MissionLookup>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StagesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
