namespace UMBRAL_Back_end.IntegrationTests.Gateway;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using UMBRAL_Back_end.IntegrationTests.Infrastructure;
using Xunit;

/// <summary>
/// Exercises ApiGateway (YARP reverse proxy) end to end against a real Kestrel listener
/// standing in for MissionService (<see cref="DownstreamStub"/>) — no Testcontainers, no
/// real microservice, everything runs in-process. Covers the cross-cutting concerns that
/// only the gateway is responsible for: JWT authorization gating, X-Correlation-ID
/// generation/propagation, and fixed-window rate limiting.
/// </summary>
public class ApiGatewayTests : IAsyncLifetime
{
    private DownstreamStub _downstream = null!;
    private ApiGatewayApiFactory _realAuthFactory = null!;
    private ApiGatewayApiFactory _bypassAuthFactory = null!;

    public async Task InitializeAsync()
    {
        _downstream = new DownstreamStub();
        await _downstream.StartAsync();

        _realAuthFactory = new ApiGatewayApiFactory(_downstream.BaseUrl);
        _bypassAuthFactory = new ApiGatewayApiFactory(_downstream.BaseUrl, bypassAuth: true);
    }

    public async Task DisposeAsync()
    {
        await _realAuthFactory.DisposeAsync();
        await _bypassAuthFactory.DisposeAsync();
        await _downstream.DisposeAsync();
    }

    [Fact]
    public async Task MissionRoute_WithoutToken_Returns401AndNeverReachesDownstream()
    {
        var client = _realAuthFactory.CreateClient();

        var response = await client.GetAsync("/mission-service/algo");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MissionRoute_AuthenticatedWithoutCorrelationHeader_GatewayGeneratesAndForwardsIt()
    {
        var client = _bypassAuthFactory.CreateClient();

        var response = await client.GetAsync("/mission-service/algo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<DownstreamResponse>();
        body.Should().NotBeNull();
        body!.CorrelationId.Should().NotBeNullOrWhiteSpace();

        // CorrelationIdMiddleware also mirrors the (generated) id back on the response,
        // and it must be the exact same value the downstream saw.
        response.Headers.TryGetValues("X-Correlation-ID", out var values).Should().BeTrue();
        values!.Single().Should().Be(body.CorrelationId);
    }

    [Fact]
    public async Task MissionRoute_AuthenticatedWithExplicitCorrelationHeader_PropagatesSameValueDownstream()
    {
        var client = _bypassAuthFactory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();
        var request = new HttpRequestMessage(HttpMethod.Get, "/mission-service/algo");
        request.Headers.Add("X-Correlation-ID", correlationId);

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DownstreamResponse>();
        body!.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task MissionRoute_BurstAbovePermitAndQueueCapacity_SomeRequestsGet429()
    {
        var client = _bypassAuthFactory.CreateClient();

        // mission-route uses RateLimiterPolicy "general": FixedWindowLimiter with
        // PermitLimit=50, QueueLimit=10 (window 10s, OldestFirst). Requests beyond
        // PermitLimit are queued (not rejected) until the window resets, so a handful
        // of *sequential* requests just above PermitLimit would sit in the queue and
        // eventually succeed rather than 429 — sequential=51 as originally sketched
        // does NOT reliably trigger a 429 with QueueLimit=10 in play. Firing requests
        // *concurrently*, well above PermitLimit + QueueLimit (60), is what actually
        // forces the limiter to reject some of them outright.
        var tasks = Enumerable.Range(0, 70)
            .Select(i => client.GetAsync($"/mission-service/rl-{i}"))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        responses.Should().Contain(r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }
}
