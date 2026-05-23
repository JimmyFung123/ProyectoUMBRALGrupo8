export type SessionStatus = 'Pending' | 'InProgress' | 'Completed' | 'Cancelled';

export const SESSION_STATUS_LABELS: Record<SessionStatus, string> = {
  Pending: 'Pendiente',
  InProgress: 'En progreso',
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

export interface Team {
  id: string;
  name: string;
  isConnected: boolean;
  currentStageOrder: number;
  cluesReceivedCurrentStage: number;
  totalCluesReceived: number;
}

export interface SessionDetail extends Session {
  teams: Team[];
}

export interface CreateSessionPayload {
  missionId: string;
  name: string;
  scheduledAt: string | null;
}
