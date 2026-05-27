namespace SessionService.Adapter.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SessionService.Application.Statistics.Queries.GetDashboardStatistics;
using UMBRAL.Auth;

/// <summary>
/// HU-25 — administrator-only statistics dashboard.
/// Reads exclusively from the <c>StageCompletionRecords</c> fact table,
/// which is populated incrementally as games are played and "promoted"
/// to dashboard visibility when a session reaches the Finalized state.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = OperatorPrincipal.AdminRole)] // HU-25: solo Administrador
public class StatisticsController : ControllerBase
{
    private readonly ISender _sender;

    public StatisticsController(ISender sender) => _sender = sender;

    /// <summary>
    /// Returns the dashboard payload (average time per stage + answer
    /// effectiveness per stage). Pass <c>missionId</c> to scope the metrics
    /// to a single mission; omit it for the global view.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] Guid? missionId,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetDashboardStatisticsQuery(missionId), cancellationToken);
        return Ok(result);
    }
}
