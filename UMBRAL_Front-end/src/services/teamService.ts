import { http } from './http';
import type { TeamProgressDto } from '../types/team';

const BASE_URL = import.meta.env.VITE_TEAM_API_URL ?? 'http://localhost:5095/api';

export const teamService = {
  getTeamProgress(sessionId: string): Promise<TeamProgressDto[]> {
    return http.get<TeamProgressDto[]>(`${BASE_URL}/teams?sessionId=${sessionId}`);
  },
};
