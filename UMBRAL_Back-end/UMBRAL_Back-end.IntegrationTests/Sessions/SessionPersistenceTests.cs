// This file touches types that live in SessionService's aliased assembly (see the
// .csproj comment on the SessionService ProjectReference for why the alias exists).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Sessions;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Adapter.Controllers;
using SessionServiceAssembly::SessionService.Domain.MissionLookup;
using SessionServiceAssembly::SessionService.Domain.Sessions;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using Xunit;

[Collection(SessionServiceCollection.Name)]
public class SessionPersistenceTests(SessionServicePostgresFixture fixture)
{
    [Fact]
    public async Task CreateSession_ValidRequestThroughHttp_PersistsInRealPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();

        // CreateSessionCommandHandler resolves the mission via IMissionLookupRepository (a
        // local projection kept in sync from MissionService via MissionCreated/Activated
        // consumers), so a MissionLookup row with Status "Active" must exist first — the
        // handler returns MissionLookupErrors.NotFound / SessionErrors.MissionNotActive
        // otherwise (SessionService never queries MissionService's DB directly).
        var missionId = Guid.NewGuid();
        var missionLookup = MissionLookup.Create(missionId, "Misión de prueba", "Active");
        dbContext.MissionsLookup.Add(missionLookup);
        await dbContext.SaveChangesAsync();

        var client = fixture.Factory.CreateClient();
        var request = new CreateSessionRequest(missionId, $"Sesión {Guid.NewGuid()}", ScheduledAt: null);

        var response = await client.PostAsJsonAsync("/api/sessions", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // CreateSessionCommandHandler returns Result<Guid>, and the controller does
        // Ok(result.Value) directly — the body is a bare JSON Guid, not an object.
        var sessionId = await response.Content.ReadFromJsonAsync<Guid>();
        sessionId.Should().NotBeEmpty();

        var persisted = await dbContext.Sessions.SingleAsync(s => s.Id == sessionId);
        persisted.MissionId.Should().Be(missionId);
        persisted.Name.Should().Be(request.Name);
        persisted.Status.Should().Be(SessionStatus.Pending);
        persisted.AccessCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SaveChanges_DuplicateAccessCode_ThrowsDueToUniqueConstraintInPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();

        // SessionConfiguration maps AccessCode as a unique index (IX_Sessions_AccessCode,
        // see the AddSessionAccessCode migration). Session.Create always generates a fresh
        // random code (SessionCode.Generate()) and nothing in the C# domain model checks
        // for collisions before SaveChanges, so this constraint only surfaces against a
        // real Postgres instance — EF Core's InMemory provider does not enforce unique
        // indexes, same category of gap exercised by the other four services' varchar/FK
        // constraint tests. It is also independent of SessionEventImmutabilityInterceptor,
        // which only inspects tracked SessionEvent entries (Modified/Deleted state) and
        // never touches the Sessions table or raw SQL inserts.
        const string sharedAccessCode = "DUP123";
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Sessions" ("Id", "MissionId", "Name", "Status", "CreatedAt", "ScheduledAt", "AccessCode")
             VALUES ({firstId}, {missionId}, 'Sesión original', 'Pending', {DateTime.UtcNow}, NULL, {sharedAccessCode})
             """);

        var act = async () => await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO "Sessions" ("Id", "MissionId", "Name", "Status", "CreatedAt", "ScheduledAt", "AccessCode")
             VALUES ({secondId}, {missionId}, 'Sesión duplicada', 'Pending', {DateTime.UtcNow}, NULL, {sharedAccessCode})
             """);

        (await act.Should().ThrowAsync<PostgresException>())
            .Which.SqlState.Should().Be("23505");
    }

    [Fact]
    public async Task Database_Migrate_AppliesAllMigrationsCleanlyAndIsIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();

        var pendingBeforeReRun = await dbContext.Database.GetPendingMigrationsAsync();
        pendingBeforeReRun.Should().BeEmpty();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var allMigrations = dbContext.Database.GetMigrations();
        appliedMigrations.Should().BeEquivalentTo(allMigrations);
        allMigrations.Should().BeEquivalentTo(new[]
        {
            "20260523185646_Initial",
            "20260523230502_AddTeams",
            "20260524001903_AddSessionEventsAndTeamScore",
            "20260524003720_DropTeams",
            "20260524210412_AddSessionAccessCode",
            "20260526120000_AddActorNameToSessionEvent",
            "20260526200024_AddStageCompletionRecords",
            "20260527050610_AddCommandMetadataToSessionEvent",
            "20260531041732_AddMissionLookupDifficulty"
        });

        var act = async () => await dbContext.Database.MigrateAsync();
        await act.Should().NotThrowAsync();

        (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }
}
