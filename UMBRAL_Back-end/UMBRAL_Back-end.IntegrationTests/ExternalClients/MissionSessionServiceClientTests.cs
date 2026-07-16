namespace UMBRAL_Back_end.IntegrationTests.ExternalClients;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using UMBRAL_Back_end.Infrastructure.ExternalClients;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Ejercita el <see cref="SessionServiceClient"/> real de MissionService (MissionService →
/// SessionService, para chequear sesiones activas antes de desactivar una misión) contra un
/// UpstreamJsonStub. Cubre el parseo del flag, la rama no-2xx (false) y —clave— el catch, que
/// falla SEGURO devolviendo <c>true</c> para no huérfanar sesiones activas si SessionService
/// no responde.
/// </summary>
public class MissionSessionServiceClientTests
{
    [Fact]
    public async Task HasActiveSessions_ReturnsTrue_WhenUpstreamSaysSo()
    {
        await using var stub = await StubHttp.Returning("""{"hasActiveSessions":true}""");
        var result = await new SessionServiceClient(StubHttp.ClientTo(stub))
            .HasActiveSessionsAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasActiveSessions_ReturnsFalse_WhenUpstreamSaysSo()
    {
        await using var stub = await StubHttp.Returning("""{"hasActiveSessions":false}""");
        var result = await new SessionServiceClient(StubHttp.ClientTo(stub))
            .HasActiveSessionsAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveSessions_ReturnsFalse_OnNonSuccessStatus()
    {
        await using var stub = await StubHttp.Returning("{}", statusCode: 500);
        var result = await new SessionServiceClient(StubHttp.ClientTo(stub))
            .HasActiveSessionsAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasActiveSessions_FailsSafeToTrue_WhenBodyIsNotValidJson()
    {
        // Cuerpo 200 pero no-JSON → JsonDocument.Parse lanza → catch → true (fail-safe).
        await using var stub = await StubHttp.Returning("no-soy-json");
        var result = await new SessionServiceClient(StubHttp.ClientTo(stub))
            .HasActiveSessionsAsync(Guid.NewGuid(), CancellationToken.None);
        result.Should().BeTrue();
    }
}
