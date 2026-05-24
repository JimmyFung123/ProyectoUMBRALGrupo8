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
}

export interface TriviaAnswerResult {
  isCorrect: boolean;
  newScore: number;
  nextStageOrder: number;
  isLastStage: boolean;
}
