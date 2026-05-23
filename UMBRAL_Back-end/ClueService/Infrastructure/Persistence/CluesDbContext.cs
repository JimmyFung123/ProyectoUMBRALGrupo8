namespace ClueService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ClueService.Domain.Clues;
using ClueService.Domain.StageLookup;

public class CluesDbContext : DbContext
{
    public CluesDbContext(DbContextOptions<CluesDbContext> options) : base(options) { }
    public DbSet<Clue> Clues => Set<Clue>();
    public DbSet<StageLookup> StagesLookup => Set<StageLookup>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CluesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
