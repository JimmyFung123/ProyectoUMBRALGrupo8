namespace UMBRAL_Back_end.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using UMBRAL_Back_end.Domain.Missions;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<MissionStage> MissionStages => Set<MissionStage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
