import type { CreateSessionPayload, Session, SessionStatus } from '../types/session';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5091/api';

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

  create(payload: CreateSessionPayload): Promise<string> {
    return fetch(`${BASE_URL}/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<string>);
  },
};
