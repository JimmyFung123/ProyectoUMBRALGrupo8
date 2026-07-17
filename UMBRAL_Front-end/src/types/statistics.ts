/**
 * Types for the HU-25 admin statistics dashboard.
 *
 * Mirror the DTOs returned by GET /api/statistics. All aggregations are
 * computed server-side over the StageCompletionRecords fact table —
 * the front never re-aggregates, it only renders.
 */

export interface StageTimeStat {
  stageOrder: number;
  /** Average seconds teams took to complete this stage across all finalized sessions. */
  averageSeconds: number;
  /** Number of completion events that fed into the average. */
  sampleSize: number;
}

export interface StageEffectivenessStat {
  stageOrder: number;
  correctCount: number;
  totalAnswers: number;
  /** Pre-computed percentage so the UI doesn't divide — already rounded to 2 decimals. */
  correctPercentage: number;
}

export interface DashboardStatistics {
  /** Null when the dashboard is showing the global view. */
  missionId: string | null;
  /** ISO timestamp the payload was generated. */
  generatedAt: string;
  averageTimePerStage: StageTimeStat[];
  effectivenessPerStage: StageEffectivenessStat[];
}
