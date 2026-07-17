// See SessionCancelledConsumerFixture.cs for why this alias is needed.
extern alias TeamServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using TeamServiceAssembly::TeamService.Domain.Rankings;
using TeamServiceAssembly::TeamService.Domain.Teams;
using TeamServiceAssembly::TeamService.Infrastructure.Persistence;
using Xunit;
using static UMBRAL_Back_end.IntegrationTests.Infrastructure.Polling;

/// <summary>
/// Level-2: publishes a single <see cref="UMBRAL.Contracts.Events.SessionCancelledIntegrationEvent"/>
/// to a real RabbitMQ broker and verifies TeamService's <c>SessionCancelledConsumer</c>
/// deletes BOTH the write-model <c>Team</c> rows AND the CQRS <c>RankingProjection</c> rows
/// (HU-24) for the cancelled session, in the same unit of work.
/// </summary>
[Collection(SessionCancelledConsumerCollection.Name)]
public class SessionCancelledConsumerTests(SessionCancelledConsumerFixture fixture)
{
    [Fact]
    public async Task PublishSessionCancelled_OverRealRabbitMq_DeletesTeamsAndRankingProjections()
    {
        var sessionId = Guid.NewGuid();
        var team = Team.Create(sessionId, TeamName.Create("Equipo Cancelado").Value);

        using (var scope = fixture.TeamFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
            db.Teams.Add(team);
            db.RankingProjections.Add(RankingProjection.Create(
                sessionId, team.Id, team.Name, score: 0, rank: 1, position: 1,
                currentStageOrder: 0, isConnected: false, lastStageCompletedAt: null, updatedAt: DateTime.UtcNow));
            await db.SaveChangesAsync();
        }

        await fixture.PublishSessionCancelledAsync(sessionId);

        var teamsRemain = await PollAsync(async () =>
        {
            using var scope = fixture.TeamFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
            return await db.Teams.AsNoTracking().AnyAsync(t => t.SessionId == sessionId);
        }, exists => !exists);

        teamsRemain.Should().BeFalse(
            "TeamService's SessionCancelledConsumer should have deleted the seeded Team row over the real broker");

        var rankingRemains = await PollAsync(async () =>
        {
            using var scope = fixture.TeamFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TeamsDbContext>();
            return await db.RankingProjections.AsNoTracking().AnyAsync(r => r.SessionId == sessionId);
        }, exists => !exists);

        rankingRemains.Should().BeFalse(
            "TeamService's SessionCancelledConsumer should have deleted the seeded RankingProjection row over the real broker");
    }
}
