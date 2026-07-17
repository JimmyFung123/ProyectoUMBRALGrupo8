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
public class SessionPersistenceTests(SessionServicePostgresFixture fixture) : IAsyncLifetime
{
    // xUnit creates a new instance of this class per [Fact], so InitializeAsync runs before
    // EACH test — unlike the collection fixture, which is shared by the whole class. Resetting
    // here keeps tests isolated from rows left over by earlier tests in the same collection.
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    // El endpoint /api/sessions está gated por [Authorize]; SessionServiceApiFactory
    // registra TestAuthHandler para que cada request autentique sin un token real de
    // Keycloak (ver Infrastructure/TestAuthHandler.cs).

    /// <summary>
    /// Siembra la réplica MissionLookup (Active) y crea una sesión por HTTP.
    /// CreateSessionCommandHandler valida la misión contra esa proyección
    /// (SessionService nunca consulta la BD de MissionService directamente).
    /// </summary>
    private async Task<Guid> CreateSessionAsync(HttpClient client, string? name = null)
    {
        var missionId = Guid.NewGuid();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            db.MissionsLookup.Add(MissionLookup.Create(missionId, "Misión de prueba", "Active"));
            await db.SaveChangesAsync();
        }

        var request = new CreateSessionRequest(missionId, name ?? $"Sesión {Guid.NewGuid()}", ScheduledAt: null);
        var response = await client.PostAsJsonAsync("/api/sessions", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private async Task<string> AccessCodeOfAsync(Guid sessionId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        return (await db.Sessions.SingleAsync(s => s.Id == sessionId)).AccessCode;
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── Create + constraint + migración (base original) ─────────────────────────

    [Fact]
    public async Task CreateSession_ValidRequestThroughHttp_PersistsInRealPostgres()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();

        var missionId = Guid.NewGuid();
        var missionLookup = MissionLookup.Create(missionId, "Misión de prueba", "Active");
        dbContext.MissionsLookup.Add(missionLookup);
        await dbContext.SaveChangesAsync();

        var client = fixture.Factory.CreateClient();
        var request = new CreateSessionRequest(missionId, $"Sesión {Guid.NewGuid()}", ScheduledAt: null);

        var response = await client.PostAsJsonAsync("/api/sessions", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

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
            "20260531041732_AddMissionLookupDifficulty",
            "20260717002729_AddCreatedByOperatorIdToSession"
        });

        var act = async () => await dbContext.Database.MigrateAsync();
        await act.Should().NotThrowAsync();

        (await dbContext.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
    }

    // ── GET /api/sessions/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetSessionById_ExistingSession_ReturnsDetail()
    {
        var client = fixture.Factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var response = await client.GetAsync($"/api/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSessionById_UnknownId_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync($"/api/sessions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sessions/by-code/{code} (anónimo) ──────────────────────────────

    [Fact]
    public async Task GetSessionByCode_ExistingCode_ReturnsSession()
    {
        var client = fixture.Factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);
        var code = await AccessCodeOfAsync(sessionId);

        var response = await client.GetAsync($"/api/sessions/by-code/{code}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSessionByCode_UnknownCode_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.GetAsync("/api/sessions/by-code/ZZZZZZ");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── GET /api/sessions (lista) ───────────────────────────────────────────────

    [Fact]
    public async Task GetSessions_ReturnsCreatedSessions()
    {
        var client = fixture.Factory.CreateClient();
        var firstId = await CreateSessionAsync(client);
        var secondId = await CreateSessionAsync(client);

        var response = await client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(firstId.ToString()).And.Contain(secondId.ToString());
    }

    // ── PUT /api/sessions/{id} (editar) ─────────────────────────────────────────

    [Fact]
    public async Task UpdateSession_PendingSession_PersistsChangeInPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var sessionId = await CreateSessionAsync(client, "Nombre Original");

        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{sessionId}", new UpdateSessionRequest("Nombre Editado", ScheduledAt: null));
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        var persisted = await dbContext.Sessions.SingleAsync(s => s.Id == sessionId);
        persisted.Name.Should().Be("Nombre Editado");
    }

    [Fact]
    public async Task UpdateSession_UnknownId_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}", new UpdateSessionRequest("X", null));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── DELETE /api/sessions/{id} (cancelar) ────────────────────────────────────

    [Fact]
    public async Task CancelSession_PendingSession_TransitionsToCancelledInPostgres()
    {
        var client = fixture.Factory.CreateClient();
        var sessionId = await CreateSessionAsync(client);

        var response = await client.DeleteAsync($"/api/sessions/{sessionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));

        using var scope = fixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        var persisted = await dbContext.Sessions.SingleAsync(s => s.Id == sessionId);
        persisted.Status.Should().Be(SessionStatus.Cancelled);
    }

    [Fact]
    public async Task CancelSession_UnknownId_ReturnsNotFound()
    {
        var client = fixture.Factory.CreateClient();

        var response = await client.DeleteAsync($"/api/sessions/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
