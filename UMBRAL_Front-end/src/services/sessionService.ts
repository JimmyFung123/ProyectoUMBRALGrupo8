import type { SessionAudit } from '../types/audit';
import type { SessionRanking } from '../types/ranking';
import type { CreateSessionPayload, Session, SessionDashboard, SessionDetail, SessionStatus } from '../types/session';
import { withOperator } from './operatorIdentity';

const BASE_URL = import.meta.env.VITE_SESSION_API_URL ?? 'http://localhost:5092/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ code: 'Unknown', message: response.statusText }));
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const sessionService = {
  getAll(missionId?: string, status?: SessionStatus): Promise<Session[]> {
    const params = new URLSearchParams();
    if (missionId) params.set('missionId', missionId);
    if (status) params.set('status', status);
    const qs = params.toString();
    return fetch(`${BASE_URL}/sessions${qs ? `?${qs}` : ''}`).then(handleResponse<Session[]>);
  },

  getById(id: string): Promise<Session> {
    return fetch(`${BASE_URL}/sessions/${id}`).then(handleResponse<Session>);
  },

  getDetail(id: string): Promise<SessionDetail> {
    return fetch(`${BASE_URL}/sessions/${id}`).then(handleResponse<SessionDetail>);
  },

  cancel(id: string): Promise<boolean> {
    return fetch(`${BASE_URL}/sessions/${id}`, {
      method: 'DELETE',
      headers: withOperator(),
    }).then(handleResponse<boolean>);
  },

  update(id: string, payload: { name: string; scheduledAt: string | null }): Promise<boolean> {
    return fetch(`${BASE_URL}/sessions/${id}`, {
      method: 'PUT',
      headers: withOperator({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(payload),
    }).then(handleResponse<boolean>);
  },

  getDashboard(id: string): Promise<SessionDashboard> {
    return fetch(`${BASE_URL}/sessions/${id}/dashboard`).then(handleResponse<SessionDashboard>);
  },

  /** HU-21: optimized read-model ranking — Score desc, resolution time as tie-breaker. */
  getRanking(id: string): Promise<SessionRanking> {
    return fetch(`${BASE_URL}/sessions/${id}/ranking`).then(handleResponse<SessionRanking>);
  },

  /** HU-22: full audit timeline for a session — chronological, every event recorded. */
  getAudit(id: string): Promise<SessionAudit> {
    return fetch(`${BASE_URL}/sessions/${id}/audit`).then(handleResponse<SessionAudit>);
  },

  create(payload: CreateSessionPayload): Promise<string> {
    return fetch(`${BASE_URL}/sessions`, {
      method: 'POST',
      headers: withOperator({ 'Content-Type': 'application/json' }),
      body: JSON.stringify(payload),
    }).then(handleResponse<string>);
  },

  start(id: string): Promise<boolean> {
    return fetch(`${BASE_URL}/sessions/${id}/start`, {
      method: 'PATCH',
      headers: withOperator(),
    }).then(handleResponse<boolean>);
  },

  pause(id: string): Promise<boolean> {
    return fetch(`${BASE_URL}/sessions/${id}/pause`, {
      method: 'PATCH',
      headers: withOperator(),
    }).then(handleResponse<boolean>);
  },

  resume(id: string): Promise<boolean> {
    return fetch(`${BASE_URL}/sessions/${id}/resume`, {
      method: 'PATCH',
      headers: withOperator(),
    }).then(handleResponse<boolean>);
  },

  finalize(id: string): Promise<boolean> {
    return fetch(`${BASE_URL}/sessions/${id}/finalize`, {
      method: 'PATCH',
      headers: withOperator(),
    }).then(handleResponse<boolean>);
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
    return fetch(`${BASE_URL}/sessions/${sessionId}/teams/${teamId}/release-clue`, {
      method: 'POST',
      headers: withOperator({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ totalCluesForStage, ...cluePayload }),
    }).then(handleResponse<boolean>);
  },

  penalizeTeam(sessionId: string, teamId: string, points: number, reason: string): Promise<{ newScore: number }> {
    return fetch(`${BASE_URL}/sessions/${sessionId}/teams/${teamId}/penalize`, {
      method: 'POST',
      headers: withOperator({ 'Content-Type': 'application/json' }),
      body: JSON.stringify({ points, reason }),
    }).then(handleResponse<{ newScore: number }>);
  },

  forceAdvanceTeam(sessionId: string, teamId: string): Promise<boolean> {
    return fetch(`${BASE_URL}/sessions/${sessionId}/teams/${teamId}/force-advance`, {
      method: 'POST',
      headers: withOperator({ 'Content-Type': 'application/json' }),
    }).then(handleResponse<boolean>);
  },
};
