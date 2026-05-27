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

// ── HU-26 — technical command audit log ─────────────────────────────────────

export interface SessionCommandAuditEntry {
  id: string;
  /** ISO timestamp with millisecond precision (criterion 1). */
  occurredAt: string;
  actorName: string;
  /** CQRS command name, e.g. "PauseSessionCommand". Nullable for legacy rows. */
  commandType: string | null;
  /** "Success" | "Failure". Nullable for legacy rows. */
  outcome: string | null;
  description: string;
}

export interface SessionCommandAudit {
  sessionId: string;
  sessionStatus: string;
  generatedAt: string;
  entries: SessionCommandAuditEntry[];
}
