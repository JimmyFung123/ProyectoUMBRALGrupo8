import { http } from './http';
import type { AddStagePayload, Stage, UpdateStagePayload } from '../types/stage';

const BASE_URL = import.meta.env.VITE_STAGE_API_URL ?? 'http://localhost:5093/api';

export const stageService = {
  getByMission(missionId: string): Promise<Stage[]> {
    return http.get<Stage[]>(`${BASE_URL}/stages?missionId=${missionId}`);
  },

  addStage(missionId: string, payload: AddStagePayload): Promise<string> {
    return http.post<string>(`${BASE_URL}/stages`, { missionId, ...payload });
  },

  updateStage(_missionId: string, stageId: string, payload: UpdateStagePayload): Promise<void> {
    return http.put<void>(`${BASE_URL}/stages/${stageId}`, payload);
  },

  removeStage(missionId: string, stageId: string): Promise<void> {
    return http.delete<void>(`${BASE_URL}/stages/${stageId}?missionId=${missionId}`);
  },

  setAutoRelease(stageId: string, payload: { timeMinutes: number | null; maxAttempts: number | null }): Promise<void> {
    return http.patch<void>(`${BASE_URL}/stages/${stageId}/auto-release`, payload);
  },
};
