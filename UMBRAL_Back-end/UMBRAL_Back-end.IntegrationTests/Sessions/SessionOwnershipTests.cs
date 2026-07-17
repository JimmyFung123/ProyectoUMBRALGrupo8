// This file touches types that live in SessionService's aliased assembly (see the
// .csproj comment on the SessionService ProjectReference for why the alias exists).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Sessions;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Adapter.Controllers;
using SessionServiceAssembly::SessionService.Domain.MissionLookup;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// RB-10 — an Operator may only act on sessions they created; Admins bypass the
/// check entirely. Uses two distinct real operator identities (real Postgres,
/// real HTTP pipeline, real SessionOwnershipFilter) instead of mocks.
/// </summary>
[Collection(SessionServiceCollection.Name)]
public class SessionOwnershipTests(SessionServicePostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpClient OperatorAClient()
        => fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.AddAuthentication(OperatorATestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, OperatorATestAuthHandler>(OperatorATestAuthHandler.SchemeName, _ => { });
        })).CreateClient();

    private HttpClient OperatorBClient()
        => fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.AddAuthentication(OperatorBTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, OperatorBTestAuthHandler>(OperatorBTestAuthHandler.SchemeName, _ => { });
        })).CreateClient();

    private HttpClient AdminClient()
        => fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.AddAuthentication(AdminTestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, AdminTestAuthHandler>(AdminTestAuthHandler.SchemeName, _ => { });
        })).CreateClient();

    /// <summary>Seeds an Active MissionLookup and creates a session as Operator A.</summary>
    private async Task<Guid> CreateSessionAsOperatorAAsync()
    {
        var missionId = Guid.NewGuid();
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            db.MissionsLookup.Add(MissionLookup.Create(missionId, "Misión de prueba", "Active"));
            await db.SaveChangesAsync();
        }

        var request = new CreateSessionRequest(missionId, $"Sesión de Operador A {Guid.NewGuid()}", ScheduledAt: null);
        using var client = OperatorAClient();
        var response = await client.PostAsJsonAsync("/api/sessions", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── Acceso a un endpoint de un solo id ──────────────────────────────────────

    [Fact]
    public async Task GetById_AsOwner_ReturnsOk()
    {
        var sessionId = await CreateSessionAsOperatorAAsync();
        using var client = OperatorAClient();

        var response = await client.GetAsync($"/api/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task GetById_AsDifferentOperator_ReturnsForbidden()
    {
        var sessionId = await CreateSessionAsOperatorAAsync();
        using var client = OperatorBClient();

        var response = await client.GetAsync($"/api/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_AsAdmin_ReturnsOk()
    {
        var sessionId = await CreateSessionAsOperatorAAsync();
        using var client = AdminClient();

        var response = await client.GetAsync($"/api/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    // ── Acción de mutación (no solo lecturas) ───────────────────────────────────

    [Fact]
    public async Task Cancel_AsDifferentOperator_ReturnsForbiddenAndDoesNotCancel()
    {
        var sessionId = await CreateSessionAsOperatorAAsync();
        using var client = OperatorBClient();

        var response = await client.DeleteAsync($"/api/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        var persisted = await db.Sessions.SingleAsync(s => s.Id == sessionId);
        persisted.Status.ToString().Should().Be("Pending");
    }

    [Fact]
    public async Task Cancel_AsOwner_ReturnsOk()
    {
        var sessionId = await CreateSessionAsOperatorAAsync();
        using var client = OperatorAClient();

        var response = await client.DeleteAsync($"/api/sessions/{sessionId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    // ── Listado (GET /api/sessions) ─────────────────────────────────────────────

    [Fact]
    public async Task GetAll_AsDifferentOperator_ExcludesOtherOperatorsSession()
    {
        var sessionId = await CreateSessionAsOperatorAAsync();
        using var client = OperatorBClient();

        var response = await client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(sessionId.ToString());
    }

    [Fact]
    public async Task GetAll_AsOwner_IncludesOwnSession()
    {
        var sessionId = await CreateSessionAsOperatorAAsync();
        using var client = OperatorAClient();

        var response = await client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(sessionId.ToString());
    }

    [Fact]
    public async Task GetAll_AsAdmin_IncludesEveryOperatorsSession()
    {
        var sessionId = await CreateSessionAsOperatorAAsync();
        using var client = AdminClient();

        var response = await client.GetAsync("/api/sessions");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain(sessionId.ToString());
    }
}
