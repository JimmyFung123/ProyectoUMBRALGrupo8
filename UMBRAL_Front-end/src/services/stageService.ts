import type { AddStagePayload, Stage, UpdateStagePayload } from '../types/stage';

const BASE_URL = import.meta.env.VITE_STAGE_API_URL ?? 'http://localhost:5093/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ code: 'Unknown', message: response.statusText }));
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const stageService = {
  getByMission(missionId: string): Promise<Stage[]> {
    return fetch(`${BASE_URL}/stages?missionId=${missionId}`).then(handleResponse<Stage[]>);
  },

  addStage(missionId: string, payload: AddStagePayload): Promise<string> {
    return fetch(`${BASE_URL}/stages`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ missionId, ...payload }),
    }).then(handleResponse<string>);
  },

  updateStage(_missionId: string, stageId: string, payload: UpdateStagePayload): Promise<void> {
    return fetch(`${BASE_URL}/stages/${stageId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<void>);
  },

  removeStage(missionId: string, stageId: string): Promise<void> {
    return fetch(`${BASE_URL}/stages/${stageId}?missionId=${missionId}`, {
      method: 'DELETE',
    }).then(handleResponse<void>);
  },

  setAutoRelease(stageId: string, payload: { timeMinutes: number | null; maxAttempts: number | null }): Promise<void> {
    return fetch(`${BASE_URL}/stages/${stageId}/auto-release`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<void>);
  },
};
