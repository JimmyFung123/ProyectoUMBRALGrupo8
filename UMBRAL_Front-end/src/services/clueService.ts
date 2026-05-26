import { http } from './http';
import type { AddCluePayload, Clue, UpdateCluePayload } from '../types/clue';

const BASE_URL = import.meta.env.VITE_CLUE_API_URL ?? 'http://localhost:5094/api';

export const clueService = {
  getClues(_missionId: string, stageId: string): Promise<Clue[]> {
    return http.get<Clue[]>(`${BASE_URL}/clues?stageId=${stageId}`);
  },

  addClue(_missionId: string, stageId: string, payload: AddCluePayload): Promise<string> {
    return http.post<string>(`${BASE_URL}/clues`, { stageId, ...payload });
  },

  updateClue(_missionId: string, _stageId: string, clueId: string, payload: UpdateCluePayload): Promise<void> {
    return http.put<void>(`${BASE_URL}/clues/${clueId}`, payload);
  },

  deleteClue(_missionId: string, _stageId: string, clueId: string): Promise<void> {
    return http.delete<void>(`${BASE_URL}/clues/${clueId}`);
  },
};
