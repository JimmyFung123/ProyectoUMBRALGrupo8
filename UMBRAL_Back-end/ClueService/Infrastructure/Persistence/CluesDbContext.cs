namespace ClueService.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ClueService.Domain.Clues;
using ClueService.Domain.Common;
using ClueService.Domain.StageLookup;

public class CluesDbContext : DbContext
{
    private readonly IMediator _mediator;

    public CluesDbContext(DbContextOptions<CluesDbContext> options, IMediator mediator) : base(options)
    {
        _mediator = mediator;
    }

    public DbSet<Clue> Clues => Set<Clue>();
    public DbSet<StageLookup> StagesLookup => Set<StageLookup>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CluesDbContext).Assembly);
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
