namespace UMBRAL_Back_end.Tests.Infrastructure;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SessionService.Domain.Sessions;
using SessionService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// HU-26 criterion 2 — SessionEvent rows must be immutable.
///
/// These tests spin up an InMemory <see cref="SessionsDbContext"/> with the
/// real interceptor wired in, then attempt the forbidden operations
/// (Modified, Deleted) to verify the interceptor blocks them.
/// </summary>
public class SessionEventImmutabilityInterceptorTests
{
    private static SessionsDbContext BuildContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<SessionsDbContext>()
            .UseInMemoryDatabase(databaseName)
            .AddInterceptors(new SessionEventImmutabilityInterceptor())
            .Options;
        return new SessionsDbContext(options);
    }

    [Fact]
    public async Task AddingNewSessionEvent_IsAllowed()
    {
        await using var ctx = BuildContext(nameof(AddingNewSessionEvent_IsAllowed));
        var evt = SessionEvent.Create(Guid.NewGuid(), "Sesión creada.");

        await ctx.SessionEvents.AddAsync(evt);

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ModifyingTrackedSessionEvent_ThrowsInvalidOperation()
    {
        await using var ctx = BuildContext(nameof(ModifyingTrackedSessionEvent_ThrowsInvalidOperation));
        var evt = SessionEvent.Create(Guid.NewGuid(), "Original");
        await ctx.SessionEvents.AddAsync(evt);
        await ctx.SaveChangesAsync();

        // Force a Modified state — the entity has private setters, but EF can
        // still flip its state through the change tracker.
        ctx.Entry(evt).State = EntityState.Modified;

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*HU-26*");
    }

    [Fact]
    public async Task DeletingSessionEvent_ThrowsInvalidOperation()
    {
        await using var ctx = BuildContext(nameof(DeletingSessionEvent_ThrowsInvalidOperation));
        var evt = SessionEvent.Create(Guid.NewGuid(), "No me borres");
        await ctx.SessionEvents.AddAsync(evt);
        await ctx.SaveChangesAsync();

        ctx.SessionEvents.Remove(evt);

        var act = async () => await ctx.SaveChangesAsync();
        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*HU-26*");
    }

    [Fact]
    public async Task SynchronousSaveChanges_AlsoEnforced()
    {
        await using var ctx = BuildContext(nameof(SynchronousSaveChanges_AlsoEnforced));
        var evt = SessionEvent.Create(Guid.NewGuid(), "Sync path");
        ctx.SessionEvents.Add(evt);
        ctx.SaveChanges();

        ctx.SessionEvents.Remove(evt);

        var act = () => ctx.SaveChanges();
        act.Should().Throw<InvalidOperationException>();
    }
}
