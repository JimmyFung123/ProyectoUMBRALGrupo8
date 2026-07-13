namespace UMBRAL_Back_end.Tests.Observability;

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UMBRAL.Observability;
using Xunit;

/// <summary>
/// Middleware de borde: asigna o reutiliza el correlation id, lo expone durante
/// la petición y lo devuelve en la respuesta.
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private static async Task<string?> InvokeCapturingContextIdAsync(HttpContext context)
    {
        string? seenDuringPipeline = null;

        var middleware = new CorrelationIdMiddleware(
            next: _ =>
            {
                seenDuringPipeline = CorrelationIdContext.Current;
                return Task.CompletedTask;
            },
            logger: NullLogger<CorrelationIdMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return seenDuringPipeline;
    }

    [Fact]
    public async Task GeneratesCorrelationId_WhenHeaderMissing()
    {
        var context = new DefaultHttpContext();

        var seen = await InvokeCapturingContextIdAsync(context);

        seen.Should().NotBeNullOrWhiteSpace();
        context.Request.Headers[CorrelationConstants.HeaderName].ToString().Should().Be(seen);
        context.Response.Headers[CorrelationConstants.HeaderName].ToString().Should().Be(seen);
    }

    [Fact]
    public async Task UsesIncomingCorrelationId_WhenHeaderPresent()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationConstants.HeaderName] = "incoming-id";

        var seen = await InvokeCapturingContextIdAsync(context);

        seen.Should().Be("incoming-id");
        context.Response.Headers[CorrelationConstants.HeaderName].ToString().Should().Be("incoming-id");
    }

    [Fact]
    public async Task ClearsContext_AfterRequest()
    {
        var context = new DefaultHttpContext();

        await InvokeCapturingContextIdAsync(context);

        CorrelationIdContext.Current.Should().BeNull();
    }
}
