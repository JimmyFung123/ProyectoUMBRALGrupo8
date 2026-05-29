/**
 * Types for the HU-27 admin sync-health dashboard.
 *
 * Mirror the DTOs returned by GET /api/sync-health. The aggregator in
 * SessionService composes one entry per CQRS read model across the system,
 * including a per-session breakdown for the live ranking projection.
 */

export type SyncHealthStatus = 'Healthy' | 'Warning' | 'Critical';

export interface RankingProjectionSession {
  sessionId: string;
  sessionStatus: string;
  teamCount: number;
  projectionCount: number;
  /** ISO timestamp. Null when the projection has no rows yet for the session. */
  lastUpdatedAt: string | null;
  lagSeconds: number | null;
  status: SyncHealthStatus;
}

export interface ProjectionHealth {
  projectionId: string;
  displayName: string;
  owningService: string;
  sourceModel: string;
  readModel: string;
  sourceCount: number;
  readCount: number;
  lastUpdatedAt: string | null;
  lagSeconds: number | null;
  status: SyncHealthStatus;
  detail: string;
  supportsReproject: boolean;
  /** True for ranking-projection — the reproject endpoint needs a sessionId. */
  requiresSessionId: boolean;
  /** Per-session detail. Populated only for the ranking card. */
  sessions: RankingProjectionSession[] | null;
}

export interface SyncHealthSnapshot {
  generatedAt: string;
  projections: ProjectionHealth[];
}

export interface ReprojectActionResult {
  projectionId: string;
  success: boolean;
  changedRows: number;
  detail: string;
  completedAt: string;
}
