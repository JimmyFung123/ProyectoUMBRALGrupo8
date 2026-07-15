// This file touches types that live in SessionService's aliased assembly (see the
// .csproj comment on the SessionService ProjectReference for why the alias exists).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Sessions;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using SessionServiceAssembly::SessionService.Adapter.Controllers;
using SessionServiceAssembly::SessionService.Application.Sessions;
using SessionServiceAssembly::SessionService.Domain.Sessions;
using SessionServiceAssembly::SessionService.Infrastructure.Persistence;
using Xunit;

/// <summary>
/// Gap #6 — SessionsController's participant-facing evidence actions
/// (SubmitTriviaAnswer, ValidateQr), both previously at zero coverage. Both run
/// through the shared EvidenceHandlerBase Template Method (validation chain →
/// hook → TeamService record → audit → broadcast), which is unit-tested in
/// isolation elsewhere — these tests exist to exercise the controller→MediatR→
/// HTTP-response wiring, not to re-derive the scoring rules.
/// </summary>
[Collection(SessionServiceCollection.Name)]
public class SessionEvidenceTests(SessionServicePostgresFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    // ── Helpers ───────────────────────────────────────────────────────────────

    // SessionServiceApiFactory already registers TestAuthHandler as the default auth
    // scheme (base ConfigureWebHost) — no need to re-register it here, only swap the
    // cross-service clients.
    private HttpClient ClientWithFakes(FakeTeamServiceClient? team = null, FakeStageServiceClient? stage = null)
        => fixture.Factory.WithWebHostBuilder(b => b.ConfigureServices(services =>
        {
            services.RemoveAll<ITeamServiceClient>();
            services.AddSingleton<ITeamServiceClient>(team ?? new FakeTeamServiceClient());

            services.RemoveAll<IStageServiceClient>();
            services.AddSingleton<IStageServiceClient>(stage ?? new FakeStageServiceClient());
        })).CreateClient();

    private async Task<Guid> SeedInProgressSessionAsync()
    {
        var session = Session.Create(Guid.NewGuid(), $"Sesión {Guid.NewGuid()}").Value;
        session.Start();

        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    private static async Task<string> Body(HttpResponseMessage r)
        => $"status inesperado; body: {await r.Content.ReadAsStringAsync()}";

    // ── POST /api/sessions/{id}/teams/{teamId}/answer-trivia (AllowAnonymous) ──

    [Fact]
    public async Task SubmitTriviaAnswer_CorrectOption_ReturnsOk()
    {
        var sessionId = await SeedInProgressSessionAsync();
        var stageId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var stageWithOptions = new StageWithOptionsInfo(
            stageId, "Trivia 1", "Trivia", Order: 1, BaseScore: 10, "¿Cuál es la capital?",
            [new TriviaOptionInfo(optionId, "Correcta", IsCorrect: true)]);

        using var client = ClientWithFakes(stage: new FakeStageServiceClient
        {
            StagesResult = [new StageInfo(stageId, Order: 1)],
            StageWithOptionsResult = stageWithOptions,
        });

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/answer-trivia",
            new SubmitTriviaAnswerRequest(stageId, optionId, ParticipantName: "Juan"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task SubmitTriviaAnswer_WrongOptionNoAttemptLimit_StillAdvancesAndReturnsOk()
    {
        // AutoReleaseMaxAttempts=null → SubmitTriviaAnswerCommandHandler always advances,
        // regardless of correctness (legacy behaviour without wrong-attempt blocking).
        var sessionId = await SeedInProgressSessionAsync();
        var stageId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        var stageWithOptions = new StageWithOptionsInfo(
            stageId, "Trivia 1", "Trivia", Order: 1, BaseScore: 10, "¿Cuál es la capital?",
            [new TriviaOptionInfo(optionId, "Incorrecta", IsCorrect: false)]);

        using var client = ClientWithFakes(stage: new FakeStageServiceClient
        {
            StagesResult = [new StageInfo(stageId, Order: 1)],
            StageWithOptionsResult = stageWithOptions,
        });

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/answer-trivia",
            new SubmitTriviaAnswerRequest(stageId, optionId));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task SubmitTriviaAnswer_UnknownOption_ReturnsBadRequest()
    {
        var sessionId = await SeedInProgressSessionAsync();
        var stageId = Guid.NewGuid();
        var stageWithOptions = new StageWithOptionsInfo(
            stageId, "Trivia 1", "Trivia", Order: 1, BaseScore: 10, "¿Cuál es la capital?",
            [new TriviaOptionInfo(Guid.NewGuid(), "Única opción", IsCorrect: true)]);

        using var client = ClientWithFakes(stage: new FakeStageServiceClient
        {
            StagesResult = [new StageInfo(stageId, Order: 1)],
            StageWithOptionsResult = stageWithOptions,
        });

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/answer-trivia",
            new SubmitTriviaAnswerRequest(stageId, OptionId: Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitTriviaAnswer_SessionNotInProgress_ReturnsBadRequest()
    {
        var session = Session.Create(Guid.NewGuid(), $"Sesión {Guid.NewGuid()}").Value; // stays Pending
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SessionsDbContext>();
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
        }

        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{session.Id}/teams/{Guid.NewGuid()}/answer-trivia",
            new SubmitTriviaAnswerRequest(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SubmitTriviaAnswer_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}/teams/{Guid.NewGuid()}/answer-trivia",
            new SubmitTriviaAnswerRequest(Guid.NewGuid(), Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── POST /api/sessions/{id}/teams/{teamId}/validate-qr (AllowAnonymous) ────

    [Fact]
    public async Task ValidateQr_CorrectCode_ReturnsOk()
    {
        var sessionId = await SeedInProgressSessionAsync();
        var stageId = Guid.NewGuid();
        var stageWithOptions = new StageWithOptionsInfo(
            stageId, "Tesoro 1", "TreasureHunt", Order: 1, BaseScore: 15, Question: null,
            Options: [], QrCode: "TESORO-123");

        using var client = ClientWithFakes(stage: new FakeStageServiceClient
        {
            StagesResult = [new StageInfo(stageId, Order: 1, Type: "TreasureHunt")],
            StageWithOptionsResult = stageWithOptions,
        });

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/validate-qr",
            new ValidateQrRequest(stageId, "TESORO-123"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task ValidateQr_WrongCode_TeamFound_ReturnsOkWithoutAdvancing()
    {
        var sessionId = await SeedInProgressSessionAsync();
        var stageId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var stageWithOptions = new StageWithOptionsInfo(
            stageId, "Tesoro 1", "TreasureHunt", Order: 1, BaseScore: 15, Question: null,
            Options: [], QrCode: "TESORO-123");

        using var client = ClientWithFakes(
            team: new FakeTeamServiceClient { TeamInfoResult = new TeamInfoItem(teamId, "Equipo Alfa", CurrentStageOrder: 1) },
            stage: new FakeStageServiceClient
            {
                StagesResult = [new StageInfo(stageId, Order: 1, Type: "TreasureHunt")],
                StageWithOptionsResult = stageWithOptions,
            });

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{teamId}/validate-qr",
            new ValidateQrRequest(stageId, "CÓDIGO-INCORRECTO"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await Body(response));
    }

    [Fact]
    public async Task ValidateQr_StageIsNotTreasureHunt_ReturnsBadRequest()
    {
        var sessionId = await SeedInProgressSessionAsync();
        var stageId = Guid.NewGuid();
        var stageWithOptions = new StageWithOptionsInfo(
            stageId, "Trivia 1", "Trivia", Order: 1, BaseScore: 10, "¿Pregunta?",
            [new TriviaOptionInfo(Guid.NewGuid(), "A", true)]);

        using var client = ClientWithFakes(stage: new FakeStageServiceClient
        {
            StagesResult = [new StageInfo(stageId, Order: 1)],
            StageWithOptionsResult = stageWithOptions,
        });

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/teams/{Guid.NewGuid()}/validate-qr",
            new ValidateQrRequest(stageId, "CUALQUIERA"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ValidateQr_UnknownSession_ReturnsNotFound()
    {
        using var client = ClientWithFakes();

        var response = await client.PostAsJsonAsync(
            $"/api/sessions/{Guid.NewGuid()}/teams/{Guid.NewGuid()}/validate-qr",
            new ValidateQrRequest(Guid.NewGuid(), "X"));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
