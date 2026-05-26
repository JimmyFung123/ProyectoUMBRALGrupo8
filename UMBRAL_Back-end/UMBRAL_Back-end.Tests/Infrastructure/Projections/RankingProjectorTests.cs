namespace UMBRAL_Back_end.Tests.Infrastructure.Projections;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TeamService.Domain.Rankings;
using TeamService.Domain.Teams;
using TeamService.Infrastructure.Persistence;
using TeamService.Infrastructure.Projections;
using Xunit;

/// <summary>
/// HU-24: the RankingProjector is where all the actual ranking logic lives now.
/// Each test spins up a fresh in-memory <see cref="TeamsDbContext"/> so the
/// upsert/refresh/delete logic can be exercised end-to-end against a real
/// change-tracker, not against a mock.
/// </summary>
public class RankingProjectorTests
{
    // ── Setup helpers ─────────────────────────────────────────────────────────

    private static TeamsDbContext NewContext() =>
        new(new DbContextOptionsBuilder<TeamsDbContext>()
            .UseInMemoryDatabase($"ranking-projector-{Guid.NewGuid()}")
            .Options);

    private static Team SeedTeam(
        TeamsDbContext ctx,
        Guid sessionId,
        string name,
        int score,
        DateTime? lastStageCompletedAt = null)
    {
        var team = Team.Create(sessionId, name);
        team.UpdateScore(score);
        if (lastStageCompletedAt.HasValue)
        {
            // Domain method always stamps UtcNow — use the private setter via
            // reflection so the test can control the tie-breaker time directly.
            var prop = typeof(Team).GetProperty(nameof(Team.LastStageCompletedAt))!;
            prop.GetSetMethod(nonPublic: true)!
                .Invoke(team, new object?[] { lastStageCompletedAt.Value });
        }
        ctx.Teams.Add(team);
        return team;
    }

    // ── Sort contract (RB-08) ─────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_OrdersTeamsByScoreDescending()
    {
        using var ctx = NewContext();
        var sessionId = Guid.NewGuid();
        SeedTeam(ctx, sessionId, "Bajo", 50);
        SeedTeam(ctx, sessionId, "Medio", 200);
        SeedTeam(ctx, sessionId, "Alto", 350);
        await ctx.SaveChangesAsync();

        var projector = new RankingProjector(ctx);
        await projector.RebuildAsync(sessionId);
        await ctx.SaveChangesAsync();

        var rows = await ctx.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.Position)
            .ToListAsync();

        rows.Select(r => r.TeamName).Should().Equal("Alto", "Medio", "Bajo");
        rows.Select(r => r.Position).Should().Equal(1, 2, 3);
        rows.Select(r => r.Rank).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task RebuildAsync_OnEqualScore_TieBreaksByEarlierResolutionTime()
    {
        using var ctx = NewContext();
        var sessionId = Guid.NewGuid();
        var earlier = new DateTime(2026, 5, 25, 10, 0, 0, DateTimeKind.Utc);
        var later   = new DateTime(2026, 5, 25, 10, 5, 0, DateTimeKind.Utc);

        SeedTeam(ctx, sessionId, "Equipo Tarde",     score: 200, lastStageCompletedAt: later);
        SeedTeam(ctx, sessionId, "Equipo Temprano",  score: 200, lastStageCompletedAt: earlier);
        await ctx.SaveChangesAsync();

        var projector = new RankingProjector(ctx);
        await projector.RebuildAsync(sessionId);
        await ctx.SaveChangesAsync();

        var rows = await ctx.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.Position)
            .ToListAsync();

        rows[0].TeamName.Should().Be("Equipo Temprano");
        rows[1].TeamName.Should().Be("Equipo Tarde");

        // Dense rank: tied scores share the same Rank.
        rows.Should().AllSatisfy(r => r.Rank.Should().Be(1));
    }

    [Fact]
    public async Task RebuildAsync_OnEqualScore_TeamsWithoutResolutionTimeGoToBottom()
    {
        using var ctx = NewContext();
        var sessionId = Guid.NewGuid();
        SeedTeam(ctx, sessionId, "Sin tiempo", score: 100, lastStageCompletedAt: null);
        SeedTeam(ctx, sessionId, "Con tiempo", score: 100,
            lastStageCompletedAt: new DateTime(2026, 5, 25, 9, 0, 0, DateTimeKind.Utc));
        await ctx.SaveChangesAsync();

        var projector = new RankingProjector(ctx);
        await projector.RebuildAsync(sessionId);
        await ctx.SaveChangesAsync();

        var rows = await ctx.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.Position)
            .ToListAsync();

        rows[0].TeamName.Should().Be("Con tiempo");
        rows[1].TeamName.Should().Be("Sin tiempo");
    }

    [Fact]
    public async Task RebuildAsync_DenseRanking_NextDistinctScoreGetsNextInteger()
    {
        // Three teams: two tied at 200, one at 100. Dense rank → 1, 1, 3.
        using var ctx = NewContext();
        var sessionId = Guid.NewGuid();
        SeedTeam(ctx, sessionId, "Tied A", 200,
            lastStageCompletedAt: new DateTime(2026, 5, 25, 9, 0, 0, DateTimeKind.Utc));
        SeedTeam(ctx, sessionId, "Tied B", 200,
            lastStageCompletedAt: new DateTime(2026, 5, 25, 9, 5, 0, DateTimeKind.Utc));
        SeedTeam(ctx, sessionId, "Solo",   100);
        await ctx.SaveChangesAsync();

        var projector = new RankingProjector(ctx);
        await projector.RebuildAsync(sessionId);
        await ctx.SaveChangesAsync();

        var rows = await ctx.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.Position)
            .ToListAsync();

        rows.Select(r => r.Rank).Should().Equal(1, 1, 3);
        rows.Select(r => r.Position).Should().Equal(1, 2, 3);
    }

    // ── Upsert semantics ──────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_OnSecondCall_RefreshesExistingRowsInPlace()
    {
        // First rebuild: Alpha leads. Second rebuild after Beta gets penalized
        // and Alpha overtakes Beta — same rows must be updated, not duplicated.
        using var ctx = NewContext();
        var sessionId = Guid.NewGuid();
        var alpha = SeedTeam(ctx, sessionId, "Alpha", 100);
        var beta  = SeedTeam(ctx, sessionId, "Beta",  200);
        await ctx.SaveChangesAsync();

        var projector = new RankingProjector(ctx);
        await projector.RebuildAsync(sessionId);
        await ctx.SaveChangesAsync();

        // Beta loses 150 points → Alpha (100) now beats Beta (50).
        beta.Penalize(150, "Misconduct");
        await projector.RebuildAsync(sessionId);
        await ctx.SaveChangesAsync();

        var rows = await ctx.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.Position)
            .ToListAsync();

        // Same row count — no duplicates from the second rebuild.
        rows.Should().HaveCount(2);
        rows[0].TeamName.Should().Be("Alpha");
        rows[0].Score.Should().Be(100);
        rows[1].TeamName.Should().Be("Beta");
        rows[1].Score.Should().Be(50);
    }

    [Fact]
    public async Task RebuildAsync_PicksUpTeamsThatAreAddedButNotYetPersisted()
    {
        // CreateTeam path: the new team is in Added state. The projector must
        // include it via the ChangeTracker because the SQL query alone wouldn't.
        using var ctx = NewContext();
        var sessionId = Guid.NewGuid();
        SeedTeam(ctx, sessionId, "Existing", 100);
        await ctx.SaveChangesAsync();

        var newcomer = Team.Create(sessionId, "Newcomer");
        ctx.Teams.Add(newcomer); // tracked as Added, not yet saved
        // Note: SaveChanges is deliberately NOT called yet — mirrors the real
        // handler flow where mutate → project → SaveChanges happens in one go.

        var projector = new RankingProjector(ctx);
        await projector.RebuildAsync(sessionId);
        await ctx.SaveChangesAsync();

        var rows = await ctx.RankingProjections
            .Where(p => p.SessionId == sessionId)
            .OrderBy(p => p.Position)
            .ToListAsync();

        rows.Should().HaveCount(2);
        rows.Select(r => r.TeamName).Should().Contain(new[] { "Existing", "Newcomer" });
    }

    // ── Session isolation ─────────────────────────────────────────────────────

    [Fact]
    public async Task RebuildAsync_DoesNotTouchOtherSessions()
    {
        using var ctx = NewContext();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        SeedTeam(ctx, sessionA, "A1", 100);
        SeedTeam(ctx, sessionB, "B1", 999);
        await ctx.SaveChangesAsync();

        var projector = new RankingProjector(ctx);
        await projector.RebuildAsync(sessionA);
        await ctx.SaveChangesAsync();

        var aRows = await ctx.RankingProjections.Where(p => p.SessionId == sessionA).ToListAsync();
        var bRows = await ctx.RankingProjections.Where(p => p.SessionId == sessionB).ToListAsync();

        aRows.Should().HaveCount(1);
        bRows.Should().BeEmpty(); // Session B was never projected
    }

    // ── Removal ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveSessionAsync_DeletesEveryRowOfTheSession()
    {
        using var ctx = NewContext();
        var sessionA = Guid.NewGuid();
        var sessionB = Guid.NewGuid();
        SeedTeam(ctx, sessionA, "A1", 100);
        SeedTeam(ctx, sessionA, "A2", 200);
        SeedTeam(ctx, sessionB, "B1", 50);
        await ctx.SaveChangesAsync();

        var projector = new RankingProjector(ctx);
        await projector.RebuildAsync(sessionA);
        await projector.RebuildAsync(sessionB);
        await ctx.SaveChangesAsync();

        await projector.RemoveSessionAsync(sessionA);
        await ctx.SaveChangesAsync();

        (await ctx.RankingProjections.AnyAsync(p => p.SessionId == sessionA)).Should().BeFalse();
        (await ctx.RankingProjections.CountAsync(p => p.SessionId == sessionB)).Should().Be(1);
    }
}
