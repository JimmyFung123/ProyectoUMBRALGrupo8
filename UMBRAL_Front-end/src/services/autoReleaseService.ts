const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5091/api';

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ code: 'Unknown', message: response.statusText }));
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const autoReleaseService = {
  configure(
    missionId: string,
    stageId: string,
    payload: { timeMinutes: number | null; maxAttempts: number | null },
  ): Promise<void> {
    return fetch(`${BASE_URL}/missions/${missionId}/stages/${stageId}/auto-release`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
    }).then(handleResponse<void>);
  },
};
