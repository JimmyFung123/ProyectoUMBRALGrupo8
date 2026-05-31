namespace SessionService.Adapter.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionService.Domain.Sessions;

/// <summary>
/// Service-to-service endpoints consumed exclusively by other UMBRAL microservices.
/// All routes are [AllowAnonymous] because callers are backend services without
/// a Keycloak token; network-level isolation (internal Docker network) is the
/// security boundary.
/// </summary>
[ApiController]
[Route("api/internal")]
[AllowAnonymous]
public class InternalController : ControllerBase
{
    private readonly ISessionRepository _sessionRepository;
    public InternalController(ISessionRepository sessionRepository) => _sessionRepository = sessionRepository;

    /// <summary>
    /// RB-15 — Used by MissionService to check whether a mission has sessions
    /// in any non-terminal state (Pending, InProgress, Paused) before allowing
    /// deactivation. Returns 200 { hasActiveSessions: true/false }.
    /// </summary>
    [HttpGet("sessions/has-active")]
    public async Task<IActionResult> HasActiveSessions(
        [FromQuery] Guid missionId,
        CancellationToken cancellationToken)
    {
        var hasActive = await _sessionRepository.HasNonTerminalSessionsAsync(missionId, cancellationToken);
        return Ok(new { hasActiveSessions = hasActive });
    }
}
