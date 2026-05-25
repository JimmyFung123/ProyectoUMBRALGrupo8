export interface SessionInfo {
  id: string;
  name: string;
  status: string;
  accessCode: string;
  missionId: string;
}

export interface TeamCreatedInfo {
  teamId: string;
  inviteCode: string;
}

export interface TeamJoinedInfo {
  teamId: string;
  teamName: string;
  inviteCode: string;
  memberCount: number;
}

export interface StageOption {
  id: string;
  text: string;
}

export interface ParticipantStage {
  stageId: string;
  title: string;
  type: string; // "Trivia" | "TreasureHunt" | "Waiting" | "Completed"
  order: number;
  question?: string;
  options: StageOption[];
  sessionStatus: string;
  currentStageOrder: number;
  isLastStage: boolean;
  latitude?: number | null;
  longitude?: number | null;
}

export interface TriviaAnswerResult {
  isCorrect: boolean;
  newScore: number;
  nextStageOrder: number;
  isLastStage: boolean;
}

export interface QrValidationResult {
  isCorrect: boolean;
  newScore: number;
  nextStageOrder: number;
  isLastStage: boolean;
}

export interface ReleasedClue {
  id: string;
  order: number;
  content: string | null;
  latitude: number | null;
  longitude: number | null;
  radiusMeters: number | null;
}

export interface ReleasedClues {
  stageId: string;
  stageOrder: number;
  stageType: string;
  cluesReceived: number;
  totalCluesForStage: number;
  clues: ReleasedClue[];
}

export type ClueStreamStatus =
  | 'connecting'
  | 'connected'
  | 'reconnecting'
  | 'disconnected';

