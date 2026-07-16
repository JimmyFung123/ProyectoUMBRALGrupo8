namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stand-in for the sibling microservice a sync-health <c>Reproject</c> action calls
/// over HTTP (e.g. StageService's stages-feed, MissionService's <c>api/missions</c>).
/// The controller resolves its target via <c>IHttpClientFactory.CreateClient()</c> and
/// a config-supplied base address — a real network address, not an in-memory
/// <c>WebApplicationFactory</c> TestServer, which isn't reachable that way — so this
/// spins up an actual Kestrel listener on a dynamic loopback port
/// (<c>http://127.0.0.1:0</c>), same approach as <see cref="Gateway.DownstreamStub"/>.
///
/// Unlike <see cref="Gateway.DownstreamStub"/> (which echoes request metadata for
/// gateway pass-through assertions), a reproject test needs the upstream service to
/// hand back a specific, caller-controlled payload — the very rows the controller is
/// expected to rebuild its local projection from — so this returns a fixed JSON body
/// instead.
/// </summary>
public sealed class UpstreamJsonStub : IAsyncDisposable
{
    private WebApplication? _app;

    /// <summary>Real, already-bound base URL (e.g. <c>http://127.0.0.1:54321</c>). Only valid after <see cref="StartAsync"/>.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync(string jsonBody, int statusCode = 200)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();

        // Responde con el mismo cuerpo a cualquier verbo (GET para los reproject; POST/PUT/DELETE
        // para los clientes HTTP de salida como TeamServiceClient). Los reproject solo hacen GET,
        // asi que sumar los demas verbos es compatible hacia atras.
        _app.MapMethods("/{**catchAll}", new[] { "GET", "POST", "PUT", "DELETE", "PATCH" },
            () => Results.Content(jsonBody, "application/json", statusCode: statusCode));

        await _app.StartAsync();

        var addressesFeature = _app.Services
            .GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>();

        BaseUrl = addressesFeature!.Addresses.First();
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }
}
