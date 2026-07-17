import { http } from './http';
import type { DashboardStatistics } from '../types/statistics';

/**
 * HU-25: admin-only statistics dashboard.
 *
 * Hits SessionService's /api/statistics endpoint, which reads exclusively
 * from the StageCompletionRecords fact table — never touches live Sessions
 * or Teams data. Authorization is enforced server-side by
 * [Authorize(Roles="admin")] in the controller.
 */

const SESSION_BASE_URL =
  import.meta.env.VITE_SESSION_API_URL ?? 'http://localhost:5092/api';

export const statisticsService = {
  /**
   * Returns the dashboard payload. Pass a mission id to scope the metrics
   * to a single mission; omit it for the global view (all missions).
   */
  getDashboard(missionId?: string | null): Promise<DashboardStatistics> {
    const query = missionId ? `?missionId=${encodeURIComponent(missionId)}` : '';
    return http.get<DashboardStatistics>(`${SESSION_BASE_URL}/statistics${query}`);
  },
};
