export type StageType = 'Trivia' | 'TreasureHunt';

export interface TriviaOption {
  id: string;
  text: string;
  isCorrect: boolean;
}

export interface Stage {
  id: string;
  missionId: string;
  title: string;
  order: number;
  type: StageType;
  baseScore: number;
  question: string | null;
  options: TriviaOption[];
  latitude: number | null;
  longitude: number | null;
  qrCode: string | null;
  autoReleaseTimeMinutes: number | null;
  autoReleaseMaxAttempts: number | null;
}

export interface AddStagePayload {
  title: string;
  order: number;
  type: StageType;
  baseScore: number;
  question?: string;
  options?: { text: string; isCorrect: boolean }[];
  latitude?: number;
  longitude?: number;
  qrCode?: string;
  autoReleaseTimeMinutes?: number;
  autoReleaseMaxAttempts?: number;
}

export interface UpdateStagePayload {
  title: string;
  order: number;
  baseScore: number;
  question?: string;
  options?: { text: string; isCorrect: boolean }[];
  latitude?: number;
  longitude?: number;
  qrCode?: string;
  autoReleaseTimeMinutes?: number;
  autoReleaseMaxAttempts?: number;
}

export interface ApiError {
  code: string;
  message: string;
}
