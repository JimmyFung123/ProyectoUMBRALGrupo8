export type MissionStatus = 'Active' | 'Inactive';

export type DifficultyLevel = 'Easy' | 'Medium' | 'Hard';

export const DIFFICULTY_LABELS: Record<DifficultyLevel, string> = {
  Easy: 'Fácil',
  Medium: 'Medio',
  Hard: 'Difícil',
};

export interface Mission {
  id: string;
  name: string;
  description: string;
  difficulty: DifficultyLevel;
  maxDuration: number;
  status: MissionStatus;
  stageCount: number;
  createdAt: string;
  updatedAt: string | null;
}

export interface CreateMissionPayload {
  name: string;
  description: string;
  difficulty: DifficultyLevel;
  maxDuration: number;
}

export interface UpdateMissionPayload {
  name: string;
  description: string;
  difficulty: DifficultyLevel;
  maxDuration: number;
}

export interface ApiError {
  code: string;
  message: string;
}
