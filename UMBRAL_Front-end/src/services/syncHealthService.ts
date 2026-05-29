import { http } from './http';
import type {
  ProjectionHealth,
  ReprojectActionResult,
  SyncHealthSnapshot,
} from '../types/syncHealth';

/**
 * HU-27 — admin-only sync-health dashboard.
 *
 * Hits SessionService's /api/sync-health, which aggregates every CQRS read
 * model in the system (the four RabbitMQ-fed replicas plus the two in-process
 * projections). All endpoints are server-side gated to the admin role.
 */
const SESSION_BASE_URL =
  import.meta.env.VITE_SESSION_API_URL ?? 'http://localhost:5092/api';

export const syncHealthService = {
  getSnapshot(): Promise<SyncHealthSnapshot> {
    return http.get<SyncHealthSnapshot>(`${SESSION_BASE_URL}/synchealth`);
  },

  reproject(projection: ProjectionHealth, sessionId?: string): Promise<ReprojectActionResult> {
    if (projection.projectionId === 'ranking-projection') {
      if (!sessionId) throw new Error('sessionId required for ranking-projection reproject');
      return http.post<ReprojectActionResult>(
        `${SESSION_BASE_URL}/synchealth/projections/ranking-projection/sessions/${encodeURIComponent(sessionId)}/reproject`,
      );
    }
    if (projection.projectionId === 'stage-completion-records') {
      return http.post<ReprojectActionResult>(
        `${SESSION_BASE_URL}/synchealth/projections/stage-completion-records/reconcile`,
      );
    }
    return http.post<ReprojectActionResult>(
      `${SESSION_BASE_URL}/synchealth/projections/${projection.projectionId}/reproject`,
    );
  },
};
