// This file touches types that live in SessionService's aliased assembly (see the
// .csproj comment on the SessionService ProjectReference for why the alias exists).
extern alias SessionServiceAssembly;

namespace UMBRAL_Back_end.IntegrationTests.Messaging;

using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using SessionServiceAssembly::SessionService.Application.Sessions;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Gap #5 — el Hub de SignalR (SessionHub, /hubs/session) no tenía ninguna prueba
/// de integración con un cliente REAL: los tests de relay verifican el
/// ISessionNotifier con un spy, pero no que un participante conectado por el Hub
/// reciba el broadcast. Acá un cliente SignalR real se conecta al TestServer (por
/// long-polling, lo más estable con WebApplicationFactory), se une al grupo de la
/// sesión e recibe el evento que dispara el notifier del lado servidor.
/// </summary>
[Collection(SessionServiceCollection.Name)]
public class SessionHubTests(SessionServicePostgresFixture fixture)
{
    [Fact]
    public async Task Hub_ClientJoinedToSessionGroup_ReceivesBroadcast()
    {
        var sessionId = Guid.NewGuid();

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(fixture.Factory.Server.BaseAddress, "hubs/session"), options =>
            {
                // El TestServer no expone WebSockets nativos; long-polling viaja por
                // el HttpMessageHandler del propio servidor de prueba.
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => fixture.Factory.Server.CreateHandler();
            })
            .Build();

        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        // Capturamos el JSON crudo para no depender del casing del protocolo.
        connection.On<JsonElement>("SessionStateChanged", payload => received.TrySetResult(payload.GetRawText()));

        await connection.StartAsync();
        await connection.InvokeAsync("JoinSession", sessionId.ToString());

        // Disparamos el broadcast desde el lado servidor, como haría un comando real.
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var notifier = scope.ServiceProvider.GetRequiredService<ISessionNotifier>();
            await notifier.NotifyStateChangedAsync(sessionId, "InProgress");
        }

        var winner = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        winner.Should().Be(received.Task, because: "un cliente unido al grupo debe recibir el broadcast del Hub");
        (await received.Task).Should().Contain("InProgress");
    }
}
