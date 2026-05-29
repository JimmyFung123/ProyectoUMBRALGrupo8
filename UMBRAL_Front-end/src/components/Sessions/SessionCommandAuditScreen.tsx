import { useEffect, useMemo, useState } from 'react';
import { connectToSessionHub } from '../../services/sessionHub';
import { sessionService } from '../../services/sessionService';
import type { SessionCommandAudit, SessionCommandAuditEntry } from '../../types/audit';
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  PageHeader,
  Select,
  Spinner,
} from '../ui';

interface Props {
  sessionId: string;
  onBack: () => void;
}

const EMPTY_STATE_STATUSES: ReadonlySet<string> = new Set(['Pending', 'Cancelled']);
const ALL_FILTER = '__ALL__';

/**
 * HU-26 — Auditoría y trazabilidad de acciones (vista técnica).
 *
 * Pantalla dedicada, NO embebida en el dashboard. Muestra el log inmutable de
 * comandos CQRS ejecutados contra la sesión.
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
    <div>
      <div className="mb-3">
        <Button variant="ghost" size="sm" onClick={onBack} leadingIcon="←">
          Volver al dashboard
        </Button>
      </div>
      <PageHeader
        eyebrow="HU-26"
        title="🔍 Auditoría completa de comandos"
        description="Log inmutable de cada comando ejecutado en esta sesión, con precisión de milisegundos. Pensado para reconstruir disputas o incidentes técnicos."
      />

      {loading && !audit && <Card><Spinner label="Cargando log…" /></Card>}
      {error && !audit && <Alert tone="danger">{error}</Alert>}

      {audit && (
        <>
          <Card className="mb-4">
            <div className="flex flex-wrap items-center gap-4 text-sm">
              <span><strong>Estado:</strong> {audit.sessionStatus}</span>
              <span><strong>Última carga:</strong> {formatTimestamp(audit.generatedAt)}</span>
              {error && <Badge tone="danger">⚠ Última actualización falló</Badge>}
            </div>
          </Card>

          {EMPTY_STATE_STATUSES.has(audit.sessionStatus) || audit.entries.length === 0 ? (
            <Card>
              <EmptyState
                icon="📭"
                title="Aún no hay comandos registrados"
                description={
                  audit.sessionStatus === 'Pending'
                    ? 'La sesión está en preparación. Los comandos comenzarán a registrarse desde el primero ejecutado.'
                    : audit.sessionStatus === 'Cancelled'
                      ? 'La sesión fue cancelada antes de iniciar.'
                      : 'Cada acción quedará registrada aquí en orden cronológico.'
                }
              />
            </Card>
          ) : (
            <Card padded={false}>
              <div className="flex flex-wrap items-center gap-3 p-4 border-b border-slate-200">
                <label className="text-sm text-ink-soft flex items-center gap-2">
                  Actor:
                  <Select
                    className="w-auto"
                    value={actorFilter}
                    onChange={e => setActorFilter(e.target.value)}
                  >
                    <option value={ALL_FILTER}>Todos</option>
                    {actorOptions.map(a => <option key={a} value={a}>{a}</option>)}
                  </Select>
                </label>
                <label className="text-sm text-ink-soft flex items-center gap-2">
                  Comando:
                  <Select
                    className="w-auto"
                    value={commandFilter}
                    onChange={e => setCommandFilter(e.target.value)}
                  >
                    <option value={ALL_FILTER}>Todos</option>
                    {commandOptions.map(c => <option key={c} value={c}>{c}</option>)}
                  </Select>
                </label>
                <span className="ml-auto text-sm font-semibold text-ink-soft">
                  {filteredEntries.length} de {audit.entries.length} {audit.entries.length === 1 ? 'comando' : 'comandos'}
                </span>
                <Button
                  size="sm"
                  onClick={handleExportCsv}
                  disabled={filteredEntries.length === 0}
                  leadingIcon="⬇"
                >
                  Exportar CSV
                </Button>
              </div>

              <div className="max-h-[600px] overflow-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="sticky top-0 bg-surface-subtle border-b-2 border-slate-300">
                      <Th>Timestamp (ms)</Th>
                      <Th>Actor</Th>
                      <Th>Command</Th>
                      <Th>Outcome</Th>
                      <Th>Descripción</Th>
                    </tr>
                  </thead>
                  <tbody>
                    {filteredEntries.map(entry => (
                      <CommandRow key={entry.id} entry={entry} />
                    ))}
                  </tbody>
                </table>
              </div>
            </Card>
          )}
        </>
      )}
    </div>
  );
}

function Th({ children }: { children: React.ReactNode }) {
  return (
    <th className="px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wider text-ink-muted">
      {children}
    </th>
  );
}

function CommandRow({ entry }: { entry: SessionCommandAuditEntry }) {
  return (
    <tr className="border-t border-slate-100">
      <td className="px-3 py-2 align-top font-mono text-xs text-ink-muted whitespace-nowrap">
        {formatTimestamp(entry.occurredAt)}
      </td>
      <td
        className="px-3 py-2 align-top font-semibold"
        style={{ color: actorColor(entry.actorName) }}
      >
        {entry.actorName}
      </td>
      <td className="px-3 py-2 align-top font-mono text-xs text-ink">
        {entry.commandType ?? '—'}
      </td>
      <td className="px-3 py-2 align-top">
        <OutcomeBadge outcome={entry.outcome ?? null} />
      </td>
      <td className="px-3 py-2 align-top text-ink-soft">{entry.description}</td>
    </tr>
  );
}

function OutcomeBadge({ outcome }: { outcome: string | null }) {
  if (outcome === 'Success') return <Badge tone="success">Success</Badge>;
  if (outcome === 'Failure') return <Badge tone="danger">Failure</Badge>;
  return <span className="text-ink-subtle">—</span>;
}

function formatTimestamp(iso: string): string {
  const d = new Date(iso);
  const date = d.toLocaleDateString('es-VE', { day: '2-digit', month: '2-digit', year: 'numeric' });
  const time = d.toLocaleTimeString('es-VE', { hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: false });
  const ms = d.getMilliseconds().toString().padStart(3, '0');
  return `${date} ${time}.${ms}`;
}

function actorColor(actorName: string): string {
  if (actorName === 'Sistema')         return '#64748b';
  if (actorName.startsWith('Equipo ')) return '#0891b2';
  return '#4338ca';
}

function escapeCsvCell(value: string): string {
  if (value.includes(',') || value.includes('"') || value.includes('\n') || value.includes('\r')) {
    return `"${value.replace(/"/g, '""')}"`;
  }
  return value;
}
