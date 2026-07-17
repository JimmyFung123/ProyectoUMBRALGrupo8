namespace StageService.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using StageService.Domain.Common;
using StageService.Domain.MissionLookup;
using StageService.Domain.Stages;

public class StagesDbContext : DbContext
{
    private readonly IMediator _mediator;

    public StagesDbContext(DbContextOptions<StagesDbContext> options, IMediator mediator) : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<Stage> Stages => Set<Stage>();
    public DbSet<TriviaOption> TriviaOptions => Set<TriviaOption>();
    public DbSet<MissionLookup> MissionsLookup => Set<MissionLookup>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StagesDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

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
