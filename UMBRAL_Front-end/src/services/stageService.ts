import type { AddStagePayload, UpdateStagePayload } from '../types/stage';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5091/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ code: 'Unknown', message: response.statusText }));
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const stageService = {
  addStage(missionId: string, payload: AddStagePayload): Promise<string> {
    return fetch(`${BASE_URL}/missions/${missionId}/stages`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<string>);
  },

  updateStage(missionId: string, stageId: string, payload: UpdateStagePayload): Promise<void> {
    return fetch(`${BASE_URL}/missions/${missionId}/stages/${stageId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<void>);
  },

  removeStage(missionId: string, stageId: string): Promise<void> {
    return fetch(`${BASE_URL}/missions/${missionId}/stages/${stageId}`, {
      method: 'DELETE',
    }).then(handleResponse<void>);
  },
};
