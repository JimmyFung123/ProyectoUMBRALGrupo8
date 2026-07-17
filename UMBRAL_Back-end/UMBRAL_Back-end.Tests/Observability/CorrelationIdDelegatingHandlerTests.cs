namespace UMBRAL_Back_end.Tests.Observability;

using System.Net;
using FluentAssertions;
using UMBRAL.Observability;
using Xunit;

/// <summary>
/// El DelegatingHandler reenvía el correlation id en las llamadas HTTP salientes
/// entre microservicios (SessionService → Team/Stage/Clue...).
/// </summary>
public class CorrelationIdDelegatingHandlerTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Captured { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Captured = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static async Task<HttpRequestMessage> SendThroughHandlerAsync(
        string? contextId, HttpRequestMessage request)
    {
        CorrelationIdContext.Current = contextId;

        var capturing = new CapturingHandler();
        using var invoker = new HttpMessageInvoker(
            new CorrelationIdDelegatingHandler { InnerHandler = capturing });

        await invoker.SendAsync(request, CancellationToken.None);
        return capturing.Captured!;
    }

    [Fact]
    public async Task AddsCorrelationHeader_FromContext()
    {
        var captured = await SendThroughHandlerAsync(
            "id-123", new HttpRequestMessage(HttpMethod.Get, "http://svc/"));

        captured.Headers.GetValues(CorrelationConstants.HeaderName)
            .Should().ContainSingle().Which.Should().Be("id-123");
    }

    [Fact]
    public async Task DoesNotAddHeader_WhenContextEmpty()
    {
        var captured = await SendThroughHandlerAsync(
            null, new HttpRequestMessage(HttpMethod.Get, "http://svc/"));

        captured.Headers.Contains(CorrelationConstants.HeaderName).Should().BeFalse();
    }

    [Fact]
    public async Task DoesNotOverride_ExistingHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "http://svc/");
        request.Headers.TryAddWithoutValidation(CorrelationConstants.HeaderName, "pre-set");

        var captured = await SendThroughHandlerAsync("id-123", request);

        captured.Headers.GetValues(CorrelationConstants.HeaderName)
            .Should().ContainSingle().Which.Should().Be("pre-set");
    }
}
