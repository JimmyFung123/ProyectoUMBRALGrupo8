namespace UMBRAL_Back_end.IntegrationTests.Gateway;

using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Stand-in for a downstream microservice sitting behind ApiGateway (YARP). YARP proxies
/// requests to a real network address — <c>WebApplicationFactory</c>'s in-memory TestServer
/// isn't reachable that way — so this spins up an actual Kestrel listener on a dynamic
/// loopback port (<c>http://127.0.0.1:0</c>) instead of using <c>WebApplicationFactory</c>.
///
/// It echoes back what it received (the <c>X-Correlation-ID</c> header and whether an
/// <c>Authorization</c> header was present) as JSON, so gateway tests can assert on
/// cross-cutting behavior (correlation id generation/propagation) without needing a real
/// MissionService running.
/// </summary>
public sealed class DownstreamStub : IAsyncDisposable
{
    private WebApplication? _app;

    /// <summary>Real, already-bound base URL (e.g. <c>http://127.0.0.1:54321</c>). Only valid after <see cref="StartAsync"/>.</summary>
    public string BaseUrl { get; private set; } = string.Empty;

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        _app = builder.Build();

        _app.MapGet("/{**catchAll}", (HttpRequest request) =>
        {
            var correlationId = request.Headers.TryGetValue("X-Correlation-ID", out var values)
                ? values.ToString()
                : null;
            var hasAuthorization = request.Headers.ContainsKey("Authorization");

            return Results.Ok(new DownstreamResponse(correlationId, hasAuthorization));
        });

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

/// <summary>JSON body <see cref="DownstreamStub"/> replies with, reflecting what it received.</summary>
public record DownstreamResponse(string? CorrelationId, bool HasAuthorization);
