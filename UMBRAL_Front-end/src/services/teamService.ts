import type { TeamProgressDto } from '../types/team';

const BASE_URL = import.meta.env.VITE_TEAM_API_URL ?? 'http://localhost:5095/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ code: 'Unknown', message: response.statusText }));
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const teamService = {
  getTeamProgress(sessionId: string): Promise<TeamProgressDto[]> {
    return fetch(`${BASE_URL}/teams?sessionId=${sessionId}`).then(handleResponse<TeamProgressDto[]>);
  },
};
