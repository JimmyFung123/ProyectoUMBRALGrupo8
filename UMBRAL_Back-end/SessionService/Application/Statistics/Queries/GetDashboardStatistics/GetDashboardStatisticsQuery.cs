namespace SessionService.Application.Statistics.Queries.GetDashboardStatistics;

using MediatR;

/// <summary>
/// Builds the admin statistics dashboard (HU-25). Optional <see cref="MissionId"/>
/// scopes the metrics to a single mission; null returns the global view.
/// </summary>
public record GetDashboardStatisticsQuery(Guid? MissionId) : IRequest<DashboardStatisticsDto>;
