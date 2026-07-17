// See SessionAuditConsumerFixture.cs for why this alias is needed.
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using Xunit;
using static UMBRAL_Back_end.IntegrationTests.Infrastructure.Polling;

/// <summary>
/// Level-2: publishes a single <see cref="UMBRAL.Contracts.Events.SessionAuditIntegrationEvent"/>
/// to a real RabbitMQ broker and verifies SessionService's <c>SessionAuditConsumer</c>
/// persists a new <c>SessionEvent</c> row. The consumer stamps <c>OccurredAt</c> itself
/// (server <c>DateTime.UtcNow</c>, not taken from the event payload) and resolves
/// <c>ActorName</c> via <c>ActorNameResolver</c> — a null/blank actor falls back to
/// "Sistema" — so this test asserts on those resolved values, not on echoing the input.
/// </summary>
[Collection(SessionAuditConsumerCollection.Name)]
public class SessionAuditConsumerTests(SessionAuditConsumerFixture fixture)
{
    [Fact]
    public async Task PublishSessionAudit_OverRealRabbitMq_PersistsSessionEventWithActorName()
    {
        var sessionId = Guid.NewGuid();
        const string description = "Operador pausó la sesión";

        await fixture.PublishSessionAuditAsync(sessionId, description, actorName: "Operador Uno", commandType: "PauseSession", outcome: "Success");

        var sessionEvent = await PollAsync(async () =>
        {
            using var scope = fixture.SessionFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            return await db.SessionEvents.AsNoTracking()
                .SingleOrDefaultAsync(e => e.SessionId == sessionId && e.Description == description);
        }, evt => evt is not null);

        sessionEvent.Should().NotBeNull(
            "SessionService's own SessionAuditConsumer should have received the event over the real broker and persisted a SessionEvent row");
        sessionEvent!.ActorName.Should().Be("Operador Uno");
        sessionEvent.CommandType.Should().Be("PauseSession");
        sessionEvent.Outcome.Should().Be("Success");
    }

    [Fact]
    public async Task PublishSessionAudit_WithNullActorName_OverRealRabbitMq_FallsBackToSistema()
    {
        var sessionId = Guid.NewGuid();
        const string description = "Clue liberada automáticamente";

        await fixture.PublishSessionAuditAsync(sessionId, description, actorName: null);

        var sessionEvent = await PollAsync(async () =>
        {
            using var scope = fixture.SessionFactory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            return await db.SessionEvents.AsNoTracking()
                .SingleOrDefaultAsync(e => e.SessionId == sessionId && e.Description == description);
        }, evt => evt is not null);

        sessionEvent.Should().NotBeNull();
        sessionEvent!.ActorName.Should().Be("Sistema",
            "SessionEvent.Create resolves a null actor to the SystemActor sentinel via ActorNameResolver");
    }
}
