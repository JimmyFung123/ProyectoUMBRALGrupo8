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

// Full detail returned by GET /api/missions/{id}
export interface TriviaOptionDetail {
  id: string;
  text: string;
  isCorrect: boolean;
}

export interface StageDetail {
  id: string;
  title: string;
  order: number;
  type: 'Trivia' | 'TreasureHunt';
  baseScore: number;
  question: string | null;
  options: TriviaOptionDetail[];
  latitude: number | null;
  longitude: number | null;
  qrCode: string | null;
  autoReleaseTimeMinutes: number | null;
  autoReleaseMaxAttempts: number | null;
}

export interface MissionDetail extends Omit<Mission, 'stageCount'> {
  stages: StageDetail[];
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
