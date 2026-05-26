import { http } from './http';
import type { CreateMissionPayload, Mission, MissionDetail, UpdateMissionPayload } from '../types/mission';

const BASE_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5091/api';

export const missionService = {
  getAll(): Promise<Mission[]> {
    return http.get<Mission[]>(`${BASE_URL}/missions`);
  },

  getById(id: string): Promise<MissionDetail> {
    return http.get<MissionDetail>(`${BASE_URL}/missions/${id}`);
  },

  create(payload: CreateMissionPayload): Promise<string> {
    return http.post<string>(`${BASE_URL}/missions`, payload);
  },

  update(id: string, payload: UpdateMissionPayload): Promise<void> {
    return http.put<void>(`${BASE_URL}/missions/${id}`, payload);
  },

  changeStatus(id: string, activate: boolean): Promise<void> {
    return http.patch<void>(`${BASE_URL}/missions/${id}/status`, { activate });
  },
};
