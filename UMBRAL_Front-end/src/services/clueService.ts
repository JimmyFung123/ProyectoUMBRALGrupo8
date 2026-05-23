import type { AddCluePayload, Clue, UpdateCluePayload } from '../types/clue';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5091/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ code: 'Unknown', message: response.statusText }));
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const clueService = {
  getClues(missionId: string, stageId: string): Promise<Clue[]> {
    return fetch(`${BASE_URL}/missions/${missionId}/stages/${stageId}/clues`)
      .then(handleResponse<Clue[]>);
  },

  addClue(missionId: string, stageId: string, payload: AddCluePayload): Promise<string> {
    return fetch(`${BASE_URL}/missions/${missionId}/stages/${stageId}/clues`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<string>);
  },

  updateClue(missionId: string, stageId: string, clueId: string, payload: UpdateCluePayload): Promise<void> {
    return fetch(`${BASE_URL}/missions/${missionId}/stages/${stageId}/clues/${clueId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<void>);
  },

  deleteClue(missionId: string, stageId: string, clueId: string): Promise<void> {
    return fetch(`${BASE_URL}/missions/${missionId}/stages/${stageId}/clues/${clueId}`, {
      method: 'DELETE',
    }).then(handleResponse<void>);
  },
};
