export interface SessionRankingTeam {
  teamId: string;
  name: string;
  score: number;
  rank: number;
  currentStageOrder: number;
  isConnected: boolean;
  /** ISO timestamp of the last legitimately-resolved stage (null if none yet). */
  lastStageCompletedAt: string | null;
}

export interface SessionRanking {
  sessionId: string;
  /** Mirrors SessionStatus enum from the backend (Pending, InProgress, etc.). */
  sessionStatus: string;
  /** ISO timestamp the snapshot was generated server-side. */
  generatedAt: string;
  teams: SessionRankingTeam[];
}
