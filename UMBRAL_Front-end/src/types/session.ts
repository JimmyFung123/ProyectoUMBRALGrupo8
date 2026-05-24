export type SessionStatus = 'Pending' | 'InProgress' | 'Paused' | 'Completed' | 'Cancelled';

export const SESSION_STATUS_LABELS: Record<SessionStatus, string> = {
  Pending: 'Pendiente',
  InProgress: 'En progreso',
  Paused: 'Pausada',
  Completed: 'Completada',
  Cancelled: 'Cancelada',
};

export interface Session {
  id: string;
  missionId: string;
  name: string;
  status: SessionStatus;
  createdAt: string;
  scheduledAt: string | null;
}

export interface SessionDetail extends Session {}

export interface CreateSessionPayload {
  missionId: string;
  name: string;
  scheduledAt: string | null;
}

// ── HU-9: Dashboard ───────────────────────────────────────────────────────────

export interface SessionEventDto {
  id: string;
  description: string;
  occurredAt: string;
}

export interface SessionDashboard {
  id: string;
  missionId: string;
  name: string;
  status: string;
  createdAt: string;
  scheduledAt: string | null;
  recentEvents: SessionEventDto[];
}
