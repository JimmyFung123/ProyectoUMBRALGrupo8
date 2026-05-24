export interface TeamProgressDto {
  id: string;
  name: string;
  isConnected: boolean;
  currentStageOrder: number;
  cluesReceivedCurrentStage: number;
  score: number;
  rank: number;
}
