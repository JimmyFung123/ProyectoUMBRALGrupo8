import type { CreateMissionPayload, Mission, UpdateMissionPayload } from '../types/mission';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5000/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ code: 'Unknown', message: response.statusText }));
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const missionService = {
  getAll(): Promise<Mission[]> {
    return fetch(`${BASE_URL}/missions`).then(handleResponse<Mission[]>);
  },

  getById(id: string): Promise<Mission> {
    return fetch(`${BASE_URL}/missions/${id}`).then(handleResponse<Mission>);
  },

  create(payload: CreateMissionPayload): Promise<string> {
    return fetch(`${BASE_URL}/missions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<string>);
  },

  update(id: string, payload: UpdateMissionPayload): Promise<void> {
    return fetch(`${BASE_URL}/missions/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<void>);
  },

  changeStatus(id: string, activate: boolean): Promise<void> {
    return fetch(`${BASE_URL}/missions/${id}/status`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ activate }),
    }).then(handleResponse<void>);
  },
};
