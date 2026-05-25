import { useEffect, useRef, useState } from 'react';
import { clueService } from '../../services/clueService';
import { connectToSessionHub } from '../../services/sessionHub';
import { sessionService } from '../../services/sessionService';
import { stageService } from '../../services/stageService';
import { teamService } from '../../services/teamService';
import { SESSION_STATUS_LABELS } from '../../types/session';
import type { SessionDashboard as SessionDashboardData, SessionEventDto, SessionStatus } from '../../types/session';
import type { Stage } from '../../types/stage';
import type { Clue } from '../../types/clue';
import type { TeamProgressDto } from '../../types/team';
import { SessionAuditTimeline } from './SessionAuditTimeline';
import { SessionControls } from './SessionControls';
import { SessionRankingPanel } from './SessionRankingPanel';
import { TeamProgressPanel } from './TeamProgressPanel';

interface Props {
  sessionId: string;
  onBack: () => void;
}

const STATUS_COLORS: Record<string, { bg: string; text: string }> = {
  Pending:    { bg: '#fff3cd', text: '#856404' },
  InProgress: { bg: '#cce5ff', text: '#004085' },
  Paused:     { bg: '#e2d9f3', text: '#4a235a' },
  Completed:  { bg: '#d4edda', text: '#155724' },
  Cancelled:  { bg: '#f8d7da', text: '#721c24' },
};

function StatusBadge({ status }: { status: string }) {
  const colors = STATUS_COLORS[status] ?? { bg: '#eee', text: '#333' };
  return (
    <span style={{
      marginLeft: '0.5rem',
      padding: '0.15rem 0.6rem',
      borderRadius: 4,
      fontSize: '0.8rem',
      fontWeight: 'bold',
      background: colors.bg,
      color: colors.text,
    }}>
      {SESSION_STATUS_LABELS[status as keyof typeof SESSION_STATUS_LABELS] ?? status}
    </span>
  );
}

/** Formats elapsed time since a given ISO date string. */
function useElapsedTime(since: string | null): string {
  const [elapsed, setElapsed] = useState('—');

  useEffect(() => {
    if (!since) return;
    function update() {
      const diffMs = Date.now() - new Date(since!).getTime();
      const totalSec = Math.floor(diffMs / 1000);
      const h = Math.floor(totalSec / 3600);
      const m = Math.floor((totalSec % 3600) / 60);
      const s = totalSec % 60;
      setElapsed(
        h > 0
          ? `${h}h ${String(m).padStart(2, '0')}m ${String(s).padStart(2, '0')}s`
          : `${String(m).padStart(2, '0')}m ${String(s).padStart(2, '0')}s`
      );
    }
    update();
    const id = setInterval(update, 1000);
    return () => clearInterval(id);
  }, [since]);

  return elapsed;
}

function MetricCard({ label, value, sub }: { label: string; value: string | number; sub?: string }) {
  return (
    <div style={{
      flex: 1,
      minWidth: 140,
      padding: '1rem',
      border: '1px solid #ddd',
      borderRadius: 8,
      textAlign: 'center',
      background: '#fafafa',
    }}>
      <div style={{ fontSize: '2rem', fontWeight: 'bold', color: '#333' }}>{value}</div>
      <div style={{ fontSize: '0.85rem', color: '#666', marginTop: '0.25rem' }}>{label}</div>
      {sub && <div style={{ fontSize: '0.75rem', color: '#999', marginTop: '0.1rem' }}>{sub}</div>}
    </div>
  );
}

function EventRow({ event }: { event: SessionEventDto }) {
  const time = new Date(event.occurredAt).toLocaleTimeString('es-VE', { timeStyle: 'medium' });
  return (
    <li style={{
      display: 'flex',
      gap: '0.75rem',
      padding: '0.5rem 0',
      borderBottom: '1px solid #f0f0f0',
      alignItems: 'flex-start',
    }}>
      <span style={{ flexShrink: 0, fontSize: '0.75rem', color: '#999', paddingTop: '0.1rem' }}>{time}</span>
      <span style={{ fontSize: '0.9rem', color: '#333' }}>{event.description}</span>
    </li>
  );
}

// ── SessionDashboard ──────────────────────────────────────────────────────────

export function SessionDashboard({ sessionId, onBack }: Props) {
  const [data, setData] = useState<SessionDashboardData | null>(null);
  const [teams, setTeams] = useState<TeamProgressDto[]>([]);
  const [stages, setStages] = useState<Stage[]>([]);
  const [cluesByStage, setCluesByStage] = useState<Record<string, Clue[]>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const elapsed = useElapsedTime(data?.createdAt ?? null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  async function loadStagesAndClues(missionId: string) {
    try {
      const stageList = await stageService.getByMission(missionId);
      const sorted = stageList.slice().sort((a, b) => a.order - b.order);
      setStages(sorted);

      const clueMap: Record<string, Clue[]> = {};
      await Promise.allSettled(
        sorted.map(async stage => {
          try {
            const clues = await clueService.getClues(missionId, stage.id);
            clueMap[stage.id] = clues.slice().sort((a, b) => a.order - b.order);
          } catch {
            clueMap[stage.id] = [];
          }
        })
      );
      setCluesByStage(clueMap);
    } catch {
      // Stages/clues unavailable — panel will show release button disabled
    }
  }

  async function load() {
    try {
      const [dashboardResult, teamsResult] = await Promise.allSettled([
        sessionService.getDashboard(sessionId),
        teamService.getTeamProgress(sessionId),
      ]);

      if (dashboardResult.status === 'rejected') {
        setError('No se pudo cargar el tablero. Reintentando…');
        return;
      }

      const dashboard = dashboardResult.value;
      setData(dashboard);
      setTeams(teamsResult.status === 'fulfilled' ? teamsResult.value : []);
      setError(teamsResult.status === 'rejected' ? '⚠ TeamService no disponible — ranking sin datos' : null);

      // Load stages + clues once we know the missionId (only when not yet loaded)
      if (stages.length === 0 && dashboard.missionId) {
        await loadStagesAndClues(dashboard.missionId);
      }
    } finally {
      setLoading(false);
    }
  }

  // ── SignalR connection ─────────────────────────────────────────────────────
  useEffect(() => {
    const hub = connectToSessionHub({ sessionId, onRefresh: () => load() });
    return () => hub.dispose();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  // ── Polling fallback ────────────────────────────────────────────────────────
  useEffect(() => {
    load();
    intervalRef.current = setInterval(load, 10_000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [sessionId]);

  if (loading) return <p style={{ padding: '2rem' }}>Cargando tablero…</p>;
  if (error && !data) return <p style={{ padding: '2rem', color: 'red' }}>{error}</p>;

  return (
    <div style={{ maxWidth: 900, margin: '0 auto', padding: '2rem', fontFamily: 'sans-serif' }}>

      {/* ── Encabezado ───────────────────────────────────────────── */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
        <button onClick={onBack} style={{ padding: '0.3rem 0.8rem', cursor: 'pointer' }}>
          ← Volver
        </button>
        <h1 style={{ margin: 0, fontSize: '1.4rem' }}>
          {data!.name}
          <StatusBadge status={data!.status} />
        </h1>
        {error && (
          <span style={{ marginLeft: 'auto', fontSize: '0.8rem', color: '#c0392b' }}>
            ⚠ Error al actualizar
          </span>
        )}
        <span style={{ marginLeft: 'auto', fontSize: '0.75rem', color: '#aaa' }}>
          Actualización automática cada 10 s
        </span>
      </div>

      {/* ── Controles de estado ──────────────────────────────────── */}
      <div style={{ marginBottom: '1.5rem' }}>
        <SessionControls
          sessionId={sessionId}
          status={data!.status as SessionStatus}
          teamsCount={teams.length}
          onStateChange={load}
        />
      </div>

      {/* ── Código de acceso para participantes ─────────────────── */}
      {data!.accessCode && (
        <div style={{
          display: 'flex', alignItems: 'center', gap: '1.5rem',
          marginBottom: '1.5rem', padding: '0.75rem 1.25rem',
          background: '#eef2ff', border: '2px solid #6366f1', borderRadius: 8,
        }}>
          <div>
            <div style={{ fontSize: '0.7rem', fontWeight: 700, color: '#6366f1', textTransform: 'uppercase', letterSpacing: '0.06em' }}>
              Código para participantes
            </div>
            <div style={{ fontSize: '1.8rem', fontWeight: 800, letterSpacing: '0.35em', color: '#3730a3', fontFamily: 'monospace', lineHeight: 1.2 }}>
              {data!.accessCode}
            </div>
          </div>
          <button
            onClick={() => navigator.clipboard.writeText(data!.accessCode)}
            style={{ marginLeft: 'auto', padding: '0.4rem 1rem', borderRadius: 6, border: '1px solid #6366f1', background: 'white', color: '#6366f1', fontWeight: 600, cursor: 'pointer' }}
          >
            Copiar
          </button>
        </div>
      )}

      {/* ── Métricas ─────────────────────────────────────────────── */}
      <div style={{ display: 'flex', gap: '1rem', flexWrap: 'wrap', marginBottom: '2rem' }}>
        <MetricCard
          label="Equipos registrados"
          value={teams.length}
          sub={teams.length === 0 ? 'Sin equipos registrados' : undefined}
        />
        <MetricCard
          label="Tiempo transcurrido"
          value={elapsed}
          sub="desde la creación de la sesión"
        />
        <MetricCard
          label="Estado"
          value={SESSION_STATUS_LABELS[data!.status as keyof typeof SESSION_STATUS_LABELS] ?? data!.status}
        />
      </div>

      {/* ── Progreso de equipos (panel operativo) ────────────────── */}
      <section style={{ border: '1px solid #ddd', borderRadius: 8, padding: '1rem', marginBottom: '1.5rem' }}>
        <h2 style={{ margin: '0 0 0.75rem', fontSize: '1rem', color: '#444' }}>
          Progreso y acciones por equipo
        </h2>
        <TeamProgressPanel
          teams={teams}
          sessionId={sessionId}
          sessionStatus={data!.status}
          stages={stages}
          cluesByStage={cluesByStage}
          onClueReleased={load}
        />
      </section>

      {/* ── Ranking en vivo (HU-21) ──────────────────────────────── */}
      <section style={{ border: '1px solid #ddd', borderRadius: 8, padding: '1rem', marginBottom: '1.5rem' }}>
        <h2 style={{ margin: '0 0 0.75rem', fontSize: '1rem', color: '#444' }}>
          🏆 Ranking en vivo
        </h2>
        <SessionRankingPanel sessionId={sessionId} />
      </section>

      {/* ── Log de eventos recientes (HU-9) ───────────────────────── */}
      <section style={{ border: '1px solid #ddd', borderRadius: 8, padding: '1rem', marginBottom: '1.5rem' }}>
        <h2 style={{ margin: '0 0 0.75rem', fontSize: '1rem', color: '#444' }}>
          Eventos recientes
        </h2>
        {data!.recentEvents.length === 0 ? (
          <p style={{ color: '#999', fontSize: '0.9rem', margin: 0 }}>
            Aún no hay eventos registrados para esta sesión.
          </p>
        ) : (
          <ul style={{ listStyle: 'none', margin: 0, padding: 0 }}>
            {data!.recentEvents.map(ev => (
              <EventRow key={ev.id} event={ev} />
            ))}
          </ul>
        )}
      </section>

      {/* ── Historial completo de auditoría (HU-22) ──────────────── */}
      <section style={{ border: '1px solid #ddd', borderRadius: 8, padding: '1rem' }}>
        <h2 style={{ margin: '0 0 0.75rem', fontSize: '1rem', color: '#444' }}>
          📜 Historial de auditoría
        </h2>
        <p style={{ margin: '0 0 0.75rem', fontSize: '0.82rem', color: '#777' }}>
          Línea de tiempo completa con quién, qué y cuándo. Útil para revisar
          reclamos o auditar la operación.
        </p>
        <SessionAuditTimeline sessionId={sessionId} />
      </section>

      {/* ── Info adicional ────────────────────────────────────────── */}
      <p style={{ marginTop: '1rem', fontSize: '0.8rem', color: '#aaa' }}>
        ID: {data!.id}
        {data!.scheduledAt && (
          <> · Programada: {new Date(data!.scheduledAt).toLocaleString('es-VE', { dateStyle: 'medium', timeStyle: 'short' })}</>
        )}
      </p>
    </div>
  );
}
