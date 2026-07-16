namespace UMBRAL_Back_end.IntegrationTests.Infrastructure;

using System;
using System.Net.Http;
using System.Threading.Tasks;

/// <summary>
/// Helpers compartidos por los tests de los clientes HTTP de salida
/// (TeamServiceClient, StageServiceClient, ClueServiceClient, SyncHealthClients,
/// SessionServiceClient). Levantan un <see cref="UpstreamJsonStub"/> con un cuerpo fijo
/// y arman un HttpClient real apuntando a su BaseUrl, para ejercitar el cliente de
/// verdad (no el fake de DI).
/// </summary>
public static class StubHttp
{
    public static async Task<UpstreamJsonStub> Returning(string json, int statusCode = 200)
    {
        var stub = new UpstreamJsonStub();
        await stub.StartAsync(json, statusCode);
        return stub;
    }

    /// <summary>HttpClient con BaseAddress terminada en '/' para que las rutas relativas del cliente resuelvan.</summary>
    public static HttpClient ClientTo(UpstreamJsonStub stub) =>
        new() { BaseAddress = new Uri(stub.BaseUrl.TrimEnd('/') + "/") };
}
