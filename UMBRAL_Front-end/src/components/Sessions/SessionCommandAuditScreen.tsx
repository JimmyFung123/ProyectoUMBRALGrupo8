import { useEffect, useMemo, useState } from 'react';
import { connectToSessionHub } from '../../services/sessionHub';
import { sessionService } from '../../services/sessionService';
import type { SessionCommandAudit, SessionCommandAuditEntry } from '../../types/audit';

interface Props {
  sessionId: string;
  onBack: () => void;
}

// HU-22 alternate-flow alignment: pending/cancelled sessions render an empty
// state instead of the table — keeps both audit views consistent.
const EMPTY_STATE_STATUSES: ReadonlySet<string> = new Set(['Pending', 'Cancelled']);

const ALL_FILTER = '__ALL__';

/**
 * HU-26 — Auditoría y trazabilidad de acciones (vista técnica).
 *
 * Pantalla dedicada, NO embebida en el dashboard. Muestra el log inmutable de
 * comandos CQRS ejecutados contra la sesión, con:
 *
 *   • Timestamp con precisión de milisegundos (criterio 1).
 *   • Tipo de comando CQRS y outcome (Success/Failure).
 *   • Filtros por actor y por tipo de comando.
 *   • Exportación CSV para reconstruir eventos en disputas.
 *
 * Refresca en tiempo real vía SignalR. La tabla es de solo lectura — la
 * inmutabilidad se garantiza también del lado backend mediante el
 * SessionEventImmutabilityInterceptor (criterio 2).
 */
export function SessionCommandAuditScreen({ sessionId, onBack }: Props) {
  const [audit, setAudit] = useState<SessionCommandAudit | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [actorFilter, setActorFilter] = useState<string>(ALL_FILTER);
  const [commandFilter, setCommandFilter] = useState<string>(ALL_FILTER);

  async function load() {
    try {
      const data = await sessionService.getCommandAudit(sessionId);
      setAudit(data);
      setError(null);
    } catch {
      setError('No se pudo cargar el log de comandos.');
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

  // Build dropdown options from the actual data — no hard-coded lists.
  const actorOptions = useMemo(() => {
    const names = new Set(audit?.entries.map(e => e.actorName) ?? []);
    return Array.from(names).sort();
  }, [audit]);

  const commandOptions = useMemo(() => {
    const names = new Set(
      (audit?.entries ?? [])
        .map(e => e.commandType)
        .filter((x): x is string => !!x),
    );
    return Array.from(names).sort();
  }, [audit]);

  const filteredEntries = useMemo(() => {
    if (!audit) return [];
    return audit.entries.filter(e =>
      (actorFilter === ALL_FILTER || e.actorName === actorFilter) &&
      (commandFilter === ALL_FILTER || (e.commandType ?? '') === commandFilter),
    );
  }, [audit, actorFilter, commandFilter]);

  function handleExportCsv() {
    if (!audit) return;
    const header = ['Timestamp UTC', 'Timestamp local', 'Actor', 'CommandType', 'Outcome', 'Descripción'];
    const rows = filteredEntries.map(e => [
      e.occurredAt,
      formatTimestamp(e.occurredAt),
      e.actorName,
      e.commandType ?? '',
      e.outcome ?? '',
      e.description,
    ]);
    const csv = [header, ...rows]
      .map(cols => cols.map(escapeCsvCell).join(','))
      .join('\r\n');

    const blob = new Blob(['﻿', csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `umbral-audit-${sessionId}-${new Date().toISOString().slice(0, 19).replace(/[:T]/g, '-')}.csv`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  return (
    <div style={styles.container}>
      <div style={styles.headerBar}>
        <button onClick={onBack} style={styles.backBtn}>← Volver al dashboard</button>
        <h1 style={styles.title}>🔍 Auditoría completa de comandos</h1>
        <p style={styles.subtitle}>
          HU-26 — log inmutable de cada comando ejecutado en esta sesión, con
          precisión de milisegundos. Pensado para reconstruir disputas o
          incidentes técnicos.
        </p>
      </div>

      {loading && !audit && <p style={styles.muted}>Cargando log…</p>}
      {error && !audit && <p style={{ ...styles.muted, color: '#c0392b' }}>{error}</p>}

      {audit && (
        <>
          <div style={styles.metaBar}>
            <span><strong>Estado:</strong> {audit.sessionStatus}</span>
            <span><strong>Última carga:</strong> {formatTimestamp(audit.generatedAt)}</span>
            {error && <span style={{ color: '#c0392b' }}>⚠ Última actualización falló</span>}
          </div>

          {EMPTY_STATE_STATUSES.has(audit.sessionStatus) || audit.entries.length === 0 ? (
            <div style={styles.emptyBox}>
              <p style={styles.emptyTitle}>📭 Aún no hay comandos registrados para esta sesión.</p>
              <p style={styles.emptySubtitle}>
                {audit.sessionStatus === 'Pending'
                  ? 'La sesión está en preparación. Los comandos comenzarán a registrarse desde el primero ejecutado.'
                  : audit.sessionStatus === 'Cancelled'
                    ? 'La sesión fue cancelada antes de iniciar.'
                    : 'Cada acción quedará registrada aquí en orden cronológico.'}
              </p>
            </div>
          ) : (
            <>
              <div style={styles.controlsBar}>
                <label style={styles.filterLabel}>
                  Actor:
                  <select
                    value={actorFilter}
                    onChange={e => setActorFilter(e.target.value)}
                    style={styles.select}
                  >
                    <option value={ALL_FILTER}>Todos</option>
                    {actorOptions.map(a => (
                      <option key={a} value={a}>{a}</option>
                    ))}
                  </select>
                </label>

                <label style={styles.filterLabel}>
                  Comando:
                  <select
                    value={commandFilter}
                    onChange={e => setCommandFilter(e.target.value)}
                    style={styles.select}
                  >
                    <option value={ALL_FILTER}>Todos</option>
                    {commandOptions.map(c => (
                      <option key={c} value={c}>{c}</option>
                    ))}
                  </select>
                </label>

                <span style={styles.count}>
                  {filteredEntries.length} de {audit.entries.length} {audit.entries.length === 1 ? 'comando' : 'comandos'}
                </span>

                <button
                  onClick={handleExportCsv}
                  disabled={filteredEntries.length === 0}
                  style={styles.exportBtn}
                >
                  ⬇ Exportar CSV
                </button>
              </div>

              <div style={styles.tableWrap}>
                <table style={styles.table}>
                  <thead>
                    <tr>
                      <th style={styles.th}>Timestamp (ms)</th>
                      <th style={styles.th}>Actor</th>
                      <th style={styles.th}>Command</th>
                      <th style={styles.th}>Outcome</th>
                      <th style={styles.th}>Descripción</th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredEntries.map(entry => (
                      <CommandRow key={entry.id} entry={entry} />
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
        </>
      )}
    </div>
  );
}

// ── Row ────────────────────────────────────────────────────────────────────

function CommandRow({ entry }: { entry: SessionCommandAuditEntry }) {
  const outcomeStyle = entry.outcome === 'Failure' ? styles.outcomeFail
    : entry.outcome === 'Success' ? styles.outcomeOk
    : styles.outcomeNeutral;
  return (
    <tr style={styles.tr}>
      <td style={{ ...styles.td, ...styles.timestamp }}>{formatTimestamp(entry.occurredAt)}</td>
      <td style={{ ...styles.td, color: actorColor(entry.actorName), fontWeight: 600 }}>{entry.actorName}</td>
      <td style={{ ...styles.td, ...styles.mono }}>{entry.commandType ?? '—'}</td>
      <td style={{ ...styles.td, ...outcomeStyle }}>{entry.outcome ?? '—'}</td>
      <td style={styles.td}>{entry.description}</td>
    </tr>
  );
}

// ── Helpers ────────────────────────────────────────────────────────────────

function formatTimestamp(iso: string): string {
  // ES-VE date + time with milliseconds (HU-26 criterion 1). Intl does not
  // expose ms — we append them manually from the Date instance.
  const d = new Date(iso);
  const date = d.toLocaleDateString('es-VE', { day: '2-digit', month: '2-digit', year: 'numeric' });
  const time = d.toLocaleTimeString('es-VE', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false });
  const ms = d.getMilliseconds().toString().padStart(3, '0');
  return `${date} ${time}.${ms}`;
}

function actorColor(actorName: string): string {
  if (actorName === 'Sistema')         return '#6b7280';
  if (actorName.startsWith('Equipo ')) return '#0891b2';
  return '#4338ca';
}

function escapeCsvCell(value: string): string {
  if (value.includes(',') || value.includes('"') || value.includes('\n') || value.includes('\r')) {
    return `"${value.replace(/"/g, '""')}"`;
  }
  return value;
}

// ── Styles ────────────────────────────────────────────────────────────────

const styles: Record<string, React.CSSProperties> = {
  container: {
    maxWidth: 1100,
    margin: '0 auto',
    padding: '2rem',
    fontFamily: 'sans-serif',
  },
  headerBar: { marginBottom: '1.5rem' },
  backBtn: {
    cursor: 'pointer',
    padding: '0.3rem 0.7rem',
    border: '1px solid #ccc',
    borderRadius: 4,
    background: '#fff',
    marginBottom: '0.75rem',
  },
  title: { margin: '0 0 0.35rem', fontSize: '1.5rem' },
  subtitle: { margin: 0, color: '#666', fontSize: '0.88rem', lineHeight: 1.5 },
  metaBar: {
    display: 'flex',
    gap: '1.5rem',
    flexWrap: 'wrap',
    padding: '0.75rem 1rem',
    background: '#f8fafc',
    border: '1px solid #e2e8f0',
    borderRadius: 6,
    marginBottom: '1rem',
    fontSize: '0.85rem',
    color: '#475569',
  },
  controlsBar: {
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: '1rem',
    marginBottom: '0.75rem',
    padding: '0.5rem 0',
  },
  filterLabel: {
    fontSize: '0.85rem',
    color: '#444',
    display: 'flex',
    alignItems: 'center',
    gap: '0.4rem',
  },
  select: {
    padding: '0.3rem 0.5rem',
    border: '1px solid #ccc',
    borderRadius: 4,
    fontSize: '0.85rem',
    background: '#fff',
  },
  count: {
    marginLeft: 'auto',
    fontSize: '0.8rem',
    color: '#666',
    fontWeight: 600,
  },
  exportBtn: {
    cursor: 'pointer',
    padding: '0.45rem 0.9rem',
    border: '1px solid #4338ca',
    borderRadius: 4,
    background: '#4338ca',
    color: '#fff',
    fontWeight: 600,
    fontSize: '0.85rem',
  },
  tableWrap: {
    border: '1px solid #e2e8f0',
    borderRadius: 6,
    overflow: 'auto',
    maxHeight: 600,
  },
  table: {
    width: '100%',
    borderCollapse: 'collapse',
    fontSize: '0.85rem',
  },
  th: {
    position: 'sticky',
    top: 0,
    background: '#f1f5f9',
    color: '#334155',
    padding: '0.6rem 0.75rem',
    textAlign: 'left',
    borderBottom: '2px solid #cbd5e1',
    fontSize: '0.78rem',
    textTransform: 'uppercase',
    letterSpacing: '0.04em',
  },
  tr: { borderBottom: '1px solid #f1f5f9' },
  td: {
    padding: '0.55rem 0.75rem',
    verticalAlign: 'top',
    color: '#1e293b',
  },
  timestamp: {
    fontFamily: 'monospace',
    fontSize: '0.78rem',
    color: '#475569',
    whiteSpace: 'nowrap',
  },
  mono: { fontFamily: 'monospace', fontSize: '0.78rem' },
  outcomeOk:      { color: '#16a34a', fontWeight: 600 },
  outcomeFail:    { color: '#dc2626', fontWeight: 600 },
  outcomeNeutral: { color: '#94a3b8' },
  emptyBox: {
    padding: '1.5rem',
    background: '#fafafa',
    border: '1px dashed #ccc',
    borderRadius: 6,
    textAlign: 'center',
  },
  emptyTitle: {
    margin: '0 0 0.5rem',
    color: '#555',
    fontSize: '1rem',
    fontWeight: 700,
  },
  emptySubtitle: { margin: 0, color: '#888', fontSize: '0.85rem' },
  muted: { color: '#999', fontSize: '0.9rem' },
};
