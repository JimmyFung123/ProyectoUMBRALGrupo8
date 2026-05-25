import { useEffect, useState } from 'react';
import { connectToSessionHub } from '../../services/sessionHub';
import { sessionService } from '../../services/sessionService';
import type { SessionAudit, SessionAuditEntry } from '../../types/audit';

// HU-22 alternate flow: "Aún no hay eventos registrados para esta sesión"
// applies when the session is still in preparation or was cancelled before
// it started. We hide the timeline in those cases regardless of whether the
// back-end has any rows (cancellation itself creates a row, but the UI must
// treat the timeline as empty per the spec).
const EMPTY_STATE_STATUSES: ReadonlySet<string> = new Set(['Pending', 'Cancelled']);

interface Props {
  sessionId: string;
}

function formatTimestamp(iso: string): string {
  // Spanish full-precision (date + time with seconds) — auditors need exactness.
  return new Date(iso).toLocaleString('es-VE', {
    dateStyle: 'short',
    timeStyle: 'medium',
  });
}

function actorColor(actorName: string): string {
  if (actorName === 'Sistema')         return '#6b7280';
  if (actorName.startsWith('Equipo ')) return '#0891b2';
  return '#4338ca'; // operadores
}

function TimelineRow({ entry }: { entry: SessionAuditEntry }) {
  return (
    <li style={styles.row}>
      <div style={styles.timeCol}>
        <span style={styles.time}>{formatTimestamp(entry.occurredAt)}</span>
      </div>
      <div style={styles.dotCol}>
        <span style={{ ...styles.dot, background: actorColor(entry.actorName) }} />
      </div>
      <div style={styles.bodyCol}>
        <div style={styles.description}>{entry.description}</div>
        <div style={{ ...styles.actor, color: actorColor(entry.actorName) }}>
          {entry.actorName}
        </div>
      </div>
    </li>
  );
}

/**
 * HU-22 — Historial de auditoría de sesión.
 *
 * Vista de solo lectura que enumera TODOS los eventos de la sesión en orden
 * cronológico (más antiguo arriba). Se refresca automáticamente con SignalR
 * cuando cambia el estado de la sesión, y vuelve a consultar el endpoint si
 * el WebSocket cae (fallback compartido con HU-21).
 */
export function SessionAuditTimeline({ sessionId }: Props) {
  const [audit, setAudit] = useState<SessionAudit | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  async function load() {
    try {
      const data = await sessionService.getAudit(sessionId);
      setAudit(data);
      setError(null);
    } catch {
      setError('No se pudo cargar el historial de auditoría.');
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
    const hub = connectToSessionHub({ sessionId, onRefresh: () => { void load(); } });
    return () => hub.dispose();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  if (loading && !audit) {
    return <p style={styles.muted}>Cargando historial…</p>;
  }

  if (error && !audit) {
    return <p style={{ ...styles.muted, color: '#c0392b' }}>{error}</p>;
  }

  if (!audit) return null;

  // HU-22 alternate flow.
  if (EMPTY_STATE_STATUSES.has(audit.sessionStatus) || audit.entries.length === 0) {
    return (
      <div style={styles.emptyBox}>
        <p style={styles.emptyTitle}>📭 Aún no hay eventos registrados para esta sesión.</p>
        <p style={styles.emptySubtitle}>
          {audit.sessionStatus === 'Pending'
            ? 'La sesión está en preparación. Los eventos comenzarán a registrarse cuando la inicies.'
            : audit.sessionStatus === 'Cancelled'
              ? 'La sesión fue cancelada antes de iniciar.'
              : 'Las acciones del operador y del sistema aparecerán aquí en orden cronológico.'}
        </p>
      </div>
    );
  }

  return (
    <div>
      <div style={styles.headerRow}>
        <span style={styles.count}>
          {audit.entries.length} {audit.entries.length === 1 ? 'evento registrado' : 'eventos registrados'}
        </span>
        {error && (
          <span style={{ fontSize: '0.78rem', color: '#c0392b' }}>
            ⚠ Última actualización falló
          </span>
        )}
      </div>
      <ul style={styles.list}>
        {audit.entries.map((e) => <TimelineRow key={e.id} entry={e} />)}
      </ul>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  headerRow: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '0.5rem',
  },
  count: {
    fontSize: '0.78rem',
    color: '#666',
    fontWeight: 600,
  },
  list: {
    listStyle: 'none',
    margin: 0,
    padding: 0,
    maxHeight: 480,
    overflowY: 'auto',
    border: '1px solid #eee',
    borderRadius: 6,
    background: '#fafafa',
  },
  row: {
    display: 'grid',
    gridTemplateColumns: 'auto 18px 1fr',
    gap: '0.6rem',
    padding: '0.55rem 0.75rem',
    borderBottom: '1px solid #f0f0f0',
    alignItems: 'start',
  },
  timeCol: {
    minWidth: 140,
    paddingTop: '0.15rem',
  },
  time: {
    fontSize: '0.75rem',
    color: '#777',
    fontFamily: 'monospace',
    whiteSpace: 'nowrap',
  },
  dotCol: {
    display: 'flex',
    justifyContent: 'center',
    paddingTop: '0.35rem',
  },
  dot: {
    display: 'inline-block',
    width: 10,
    height: 10,
    borderRadius: '50%',
  },
  bodyCol: { minWidth: 0 },
  description: {
    fontSize: '0.88rem',
    color: '#222',
    lineHeight: 1.4,
    wordBreak: 'break-word',
  },
  actor: {
    fontSize: '0.72rem',
    fontWeight: 700,
    marginTop: '0.15rem',
    textTransform: 'uppercase',
    letterSpacing: '0.04em',
  },
  emptyBox: {
    padding: '1.25rem',
    background: '#fafafa',
    border: '1px dashed #ccc',
    borderRadius: 6,
    textAlign: 'center',
  },
  emptyTitle: {
    margin: '0 0 0.35rem',
    color: '#555',
    fontSize: '0.95rem',
    fontWeight: 700,
  },
  emptySubtitle: {
    margin: 0,
    color: '#888',
    fontSize: '0.82rem',
  },
  muted: { color: '#999', fontSize: '0.9rem', margin: 0 },
};
