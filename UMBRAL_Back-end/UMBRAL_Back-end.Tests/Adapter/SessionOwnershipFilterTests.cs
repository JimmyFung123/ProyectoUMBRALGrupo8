namespace UMBRAL_Back_end.Tests.Adapter;

using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Moq;
using SessionService.Adapter.Filters;
using SessionService.Domain.Sessions;
using Xunit;

/// <summary>RB-10: an Operator may only act on sessions they created; Admins bypass the check.</summary>
public class SessionOwnershipFilterTests
{
    private readonly Mock<ISessionRepository> _repoMock = new();
    private readonly SessionOwnershipFilter _filter;

    public SessionOwnershipFilterTests()
        => _filter = new SessionOwnershipFilter(_repoMock.Object);

    private static ClaimsPrincipal BuildUser(bool isAdmin, string? sub)
    {
        var claims = new List<Claim>();
        if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, "admin"));
        if (sub is not null) claims.Add(new Claim("sub", sub));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
    }

    private static ActionExecutingContext BuildContext(ClaimsPrincipal user, Guid? routeId)
    {
        var httpContext = new DefaultHttpContext { User = user };
        var routeData = new RouteData();
        if (routeId.HasValue) routeData.Values["id"] = routeId.Value.ToString();

        var actionContext = new ActionContext(httpContext, routeData, new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());
    }

    private static (ActionExecutionDelegate next, Func<bool> wasCalled) BuildNext(ActionExecutingContext context)
    {
        var called = false;
        Task<ActionExecutedContext> Next()
        {
            called = true;
            // ActionExecutingContext IS an ActionContext (via FilterContext), so it
            // can be passed directly as the ActionExecutedContext's ActionContext.
            return Task.FromResult(new ActionExecutedContext(
                context, new List<IFilterMetadata>(), context.Controller));
        }
        return (Next, () => called);
    }

    [Fact]
    public async Task Admin_BypassesCheck_WithoutTouchingRepository()
    {
        var context = BuildContext(BuildUser(isAdmin: true, sub: "admin-sub"), Guid.NewGuid());
        var (next, wasCalled) = BuildNext(context);

        await _filter.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        context.Result.Should().BeNull();
        _repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NoIdInRoute_SkipsCheck()
    {
        // GetAll and Create have no "{id}" route segment.
        var context = BuildContext(BuildUser(isAdmin: false, sub: "operator-1"), routeId: null);
        var (next, wasCalled) = BuildNext(context);

        await _filter.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        context.Result.Should().BeNull();
        _repoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SessionNotFound_LetsActionHandleIt()
    {
        var sessionId = Guid.NewGuid();
        _repoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync((Session?)null);
        var context = BuildContext(BuildUser(isAdmin: false, sub: "operator-1"), sessionId);
        var (next, wasCalled) = BuildNext(context);

        await _filter.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task Owner_IsAllowedThrough()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión", null, "operator-1").Value;
        _repoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var context = BuildContext(BuildUser(isAdmin: false, sub: "operator-1"), sessionId);
        var (next, wasCalled) = BuildNext(context);

        await _filter.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task LegacySessionWithNoRecordedOwner_IsAllowedThrough()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión anterior a RB-10").Value; // CreatedByOperatorId == null
        _repoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var context = BuildContext(BuildUser(isAdmin: false, sub: "operator-1"), sessionId);
        var (next, wasCalled) = BuildNext(context);

        await _filter.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeTrue();
        context.Result.Should().BeNull();
    }

    [Fact]
    public async Task NonOwner_IsBlockedWith403()
    {
        var sessionId = Guid.NewGuid();
        var session = Session.Create(Guid.NewGuid(), "Sesión de otro operador", null, "owner-sub").Value;
        _repoMock.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var context = BuildContext(BuildUser(isAdmin: false, sub: "intruder-sub"), sessionId);
        var (next, wasCalled) = BuildNext(context);

        await _filter.OnActionExecutionAsync(context, next);

        wasCalled().Should().BeFalse();
        context.Result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
