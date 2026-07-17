import { useEffect, useState } from 'react';
import { connectToSessionHub } from '../../services/sessionHub';
import { sessionService } from '../../services/sessionService';
import type { SessionAudit, SessionAuditEntry } from '../../types/audit';
import { Alert, Badge, EmptyState, Spinner } from '../ui';

const EMPTY_STATE_STATUSES: ReadonlySet<string> = new Set(['Pending', 'Cancelled']);

interface Props { sessionId: string }

function formatTimestamp(iso: string): string {
  return new Date(iso).toLocaleString('es-VE', { dateStyle: 'short', timeStyle: 'medium' });
}

function actorColor(actorName: string): string {
  if (actorName === 'Sistema')         return '#64748b';
  if (actorName.startsWith('Equipo ')) return '#0891b2';
  return '#4338ca';
}

function TimelineRow({ entry }: { entry: SessionAuditEntry }) {
  const color = actorColor(entry.actorName);
  return (
    <li className="grid grid-cols-[auto_18px_1fr] gap-2 px-3 py-2.5 border-b border-slate-100 last:border-b-0">
      <div className="min-w-[140px] pt-0.5">
        <span className="font-mono text-[0.72rem] text-ink-muted whitespace-nowrap">
          {formatTimestamp(entry.occurredAt)}
        </span>
      </div>
      <div className="flex justify-center pt-1.5">
        <span
          className="inline-block w-2.5 h-2.5 rounded-full"
          style={{ background: color }}
        />
      </div>
      <div className="min-w-0">
        <div className="text-sm text-ink leading-snug break-words">{entry.description}</div>
        <div
          className="text-[0.7rem] font-bold uppercase tracking-wider mt-0.5"
          style={{ color }}
        >
          {entry.actorName}
        </div>
      </div>
    </li>
  );
}

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

  if (loading && !audit) return <Spinner size="sm" label="Cargando historial…" />;
  if (error && !audit)   return <Alert tone="danger">{error}</Alert>;
  if (!audit) return null;

  if (EMPTY_STATE_STATUSES.has(audit.sessionStatus) || audit.entries.length === 0) {
    return (
      <EmptyState
        icon="📭"
        title="Aún no hay eventos registrados"
        description={
          audit.sessionStatus === 'Pending'
            ? 'La sesión está en preparación. Los eventos comenzarán a registrarse cuando la inicies.'
            : audit.sessionStatus === 'Cancelled'
              ? 'La sesión fue cancelada antes de iniciar.'
              : 'Las acciones del operador y del sistema aparecerán aquí en orden cronológico.'
        }
      />
    );
  }

  return (
    <div>
      <div className="flex items-center justify-between mb-2">
        <span className="text-xs font-semibold text-ink-muted">
          {audit.entries.length} {audit.entries.length === 1 ? 'evento registrado' : 'eventos registrados'}
        </span>
        {error && <Badge tone="danger">⚠ Última actualización falló</Badge>}
      </div>
      <ul className="list-none m-0 p-0 max-h-[480px] overflow-y-auto border border-slate-200 rounded bg-surface-subtle">
        {audit.entries.map(e => <TimelineRow key={e.id} entry={e} />)}
      </ul>
    </div>
  );
}
