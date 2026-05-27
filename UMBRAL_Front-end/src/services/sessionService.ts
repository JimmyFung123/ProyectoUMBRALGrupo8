import { http } from './http';
import type { SessionAudit, SessionCommandAudit } from '../types/audit';
import type { SessionRanking } from '../types/ranking';
import type { CreateSessionPayload, Session, SessionDashboard, SessionDetail, SessionStatus } from '../types/session';

const BASE_URL = import.meta.env.VITE_SESSION_API_URL ?? 'http://localhost:5092/api';

/**
 * Todos los métodos pasan por el helper `http`, que adjunta automáticamente
 * `Authorization: Bearer <access_token>` cuando hay sesión activa en Keycloak.
 * El header legacy `X-Operator-Name` ya no se usa — el backend toma el actor
 * desde el JWT.
 */
export const sessionService = {
  getAll(missionId?: string, status?: SessionStatus): Promise<Session[]> {
    const params = new URLSearchParams();
    if (missionId) params.set('missionId', missionId);
    if (status) params.set('status', status);
    const qs = params.toString();
    return http.get<Session[]>(`${BASE_URL}/sessions${qs ? `?${qs}` : ''}`);
  },

  getById(id: string): Promise<Session> {
    return http.get<Session>(`${BASE_URL}/sessions/${id}`);
  },

  getDetail(id: string): Promise<SessionDetail> {
    return http.get<SessionDetail>(`${BASE_URL}/sessions/${id}`);
  },

  cancel(id: string): Promise<boolean> {
    return http.delete<boolean>(`${BASE_URL}/sessions/${id}`);
  },

  update(id: string, payload: { name: string; scheduledAt: string | null }): Promise<boolean> {
    return http.put<boolean>(`${BASE_URL}/sessions/${id}`, payload);
  },

  getDashboard(id: string): Promise<SessionDashboard> {
    return http.get<SessionDashboard>(`${BASE_URL}/sessions/${id}/dashboard`);
  },

  /** HU-21: optimized read-model ranking — Score desc, resolution time as tie-breaker. */
  getRanking(id: string): Promise<SessionRanking> {
    return http.get<SessionRanking>(`${BASE_URL}/sessions/${id}/ranking`);
  },

  /** HU-22: full audit timeline for a session — chronological, every event recorded. */
  getAudit(id: string): Promise<SessionAudit> {
    return http.get<SessionAudit>(`${BASE_URL}/sessions/${id}/audit`);
  },

  /**
   * HU-26: technical command audit log — every CQRS command executed against
   * the session with millisecond-precise timestamps, command type and outcome.
   * Used by the dedicated "Auditoría completa" screen.
   */
  getCommandAudit(id: string): Promise<SessionCommandAudit> {
    return http.get<SessionCommandAudit>(`${BASE_URL}/sessions/${id}/audit-log`);
  },

  create(payload: CreateSessionPayload): Promise<string> {
    return http.post<string>(`${BASE_URL}/sessions`, payload);
  },

  start(id: string): Promise<boolean> {
    return http.patch<boolean>(`${BASE_URL}/sessions/${id}/start`);
  },

  pause(id: string): Promise<boolean> {
    return http.patch<boolean>(`${BASE_URL}/sessions/${id}/pause`);
  },

  resume(id: string): Promise<boolean> {
    return http.patch<boolean>(`${BASE_URL}/sessions/${id}/resume`);
  },

  finalize(id: string): Promise<boolean> {
    return http.patch<boolean>(`${BASE_URL}/sessions/${id}/finalize`);
  },

  releaseClue(
    sessionId: string,
    teamId: string,
    totalCluesForStage: number,
    cluePayload: {
      clueContent?: string | null;
      clueLatitude?: number | null;
      clueLongitude?: number | null;
      clueRadiusMeters?: number | null;
    },
  ): Promise<boolean> {
    return http.post<boolean>(
      `${BASE_URL}/sessions/${sessionId}/teams/${teamId}/release-clue`,
      { totalCluesForStage, ...cluePayload },
    );
  },

  penalizeTeam(sessionId: string, teamId: string, points: number, reason: string): Promise<{ newScore: number }> {
    return http.post<{ newScore: number }>(
      `${BASE_URL}/sessions/${sessionId}/teams/${teamId}/penalize`,
      { points, reason },
    );
  },

  forceAdvanceTeam(sessionId: string, teamId: string): Promise<boolean> {
    return http.post<boolean>(
      `${BASE_URL}/sessions/${sessionId}/teams/${teamId}/force-advance`,
    );
  },
};
