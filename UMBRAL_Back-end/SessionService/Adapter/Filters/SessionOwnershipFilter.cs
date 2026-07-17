namespace SessionService.Adapter.Filters;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SessionService.Domain.Common;
using SessionService.Domain.Sessions;
using UMBRAL.Auth;

/// <summary>
/// Enforces RB-10: an Operator may only act on sessions they created.
/// Admins bypass this check entirely. Resolves the session id from the
/// action's "id" route value — actions without that route value (GetAll,
/// Create) are unaffected. Sessions created before RB-10 existed have a
/// null <see cref="Session.CreatedByOperatorId"/> and are treated as
/// unrestricted, since there is no recorded owner to enforce against.
/// </summary>
public class SessionOwnershipFilter : IAsyncActionFilter
{
    private readonly ISessionRepository _sessionRepository;

    public SessionOwnershipFilter(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.IsAdmin())
        {
            await next();
            return;
        }

        if (!context.RouteData.Values.TryGetValue("id", out var idValue) ||
            !Guid.TryParse(idValue?.ToString(), out var sessionId))
        {
            await next();
            return;
        }

        var session = await _sessionRepository.GetByIdAsync(sessionId, context.HttpContext.RequestAborted);
        if (session is null)
        {
            // Let the action's own not-found handling run.
            await next();
            return;
        }

        var operatorId = user.GetOperatorId();
        if (session.CreatedByOperatorId is not null && session.CreatedByOperatorId != operatorId)
        {
            context.Result = new ObjectResult(new Error("Forbidden", "No tenés permiso para administrar esta sesión."))
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }
}
