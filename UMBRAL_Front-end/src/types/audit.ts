export interface SessionAuditEntry {
  id: string;
  description: string;
  actorName: string;
  /** ISO timestamp. */
  occurredAt: string;
}

export interface SessionAudit {
  sessionId: string;
  sessionStatus: string;
  entries: SessionAuditEntry[];
}
