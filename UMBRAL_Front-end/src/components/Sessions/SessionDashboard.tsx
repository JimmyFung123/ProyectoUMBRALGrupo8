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
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  Spinner,
  Stack,
  type BadgeTone,
} from '../ui';

interface Props {
  sessionId: string;
  onBack: () => void;
  /** HU-26 — navega a la pantalla de auditoría técnica completa. */
  onOpenCommandAudit?: () => void;
}

const STATUS_TONES: Record<string, BadgeTone> = {
  Pending:    'warning',
  InProgress: 'brand',
  Paused:     'info',
  Completed:  'success',
  Cancelled:  'danger',
};

function StatusBadge({ status }: { status: string }) {
  return (
    <Badge tone={STATUS_TONES[status] ?? 'neutral'} variant="solid">
      {SESSION_STATUS_LABELS[status as keyof typeof SESSION_STATUS_LABELS] ?? status}
    </Badge>
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
          : `${String(m).padStart(2, '0')}m ${String(s).padStart(2, '0')}s`,
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
    <Card padded={false} className="flex-1 min-w-[140px] p-4 text-center bg-surface-inset">
      <div className="text-3xl font-bold text-ink">{value}</div>
      <div className="text-xs text-ink-muted mt-1 font-medium uppercase tracking-wider">{label}</div>
      {sub && <div className="text-xs text-ink-subtle mt-0.5">{sub}</div>}
    </Card>
  );
}

function EventRow({ event }: { event: SessionEventDto }) {
  const time = new Date(event.occurredAt).toLocaleTimeString('es-VE', { timeStyle: 'medium' });
  return (
    <li className="flex gap-3 py-2 border-b border-slate-100 last:border-b-0 items-start">
      <span className="shrink-0 text-xs text-ink-subtle font-mono pt-0.5">{time}</span>
      <span className="text-sm text-ink-soft">{event.description}</span>
    </li>
  );
}

// ── SessionDashboard ──────────────────────────────────────────────────────────

export function SessionDashboard({ sessionId, onBack, onOpenCommandAudit }: Props) {
  const [data, setData] = useState<SessionDashboardData | null>(null);
  const [teams, setTeams] = useState<TeamProgressDto[]>([]);
  const [stages, setStages] = useState<Stage[]>([]);
  const [cluesByStage, setCluesByStage] = useState<Record<string, Clue[]>>({});
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
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
          } catch { clueMap[stage.id] = []; }
        }),
      );
      setCluesByStage(clueMap);
    } catch { /* Stages/clues unavailable — release button will be disabled */ }
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
      if (stages.length === 0 && dashboard.missionId) {
        await loadStagesAndClues(dashboard.missionId);
      }
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    const hub = connectToSessionHub({ sessionId, onRefresh: () => load() });
    return () => hub.dispose();
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  useEffect(() => {
    load();
    intervalRef.current = setInterval(load, 10_000);
    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  async function copyCode() {
    if (!data?.accessCode) return;
    try {
      await navigator.clipboard.writeText(data.accessCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch { /* ignored */ }
  }

  if (loading) return <Card><Spinner label="Cargando tablero…" /></Card>;
  if (error && !data) return <Alert tone="danger">{error}</Alert>;

  return (
    <div>
      {/* ── Encabezado ───────────────────────────────────────────────────── */}
      <div className="flex items-start gap-4 mb-5 flex-wrap">
        <Button variant="ghost" size="sm" onClick={onBack} leadingIcon="←">
          Volver
        </Button>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <h1 className="text-xl md:text-2xl font-bold text-ink leading-tight">{data!.name}</h1>
            <StatusBadge status={data!.status} />
          </div>
          <p className="text-xs text-ink-muted mt-1">
            Actualización automática cada 10 s · ID {data!.id}
            {data!.scheduledAt && (
              <> · Programada para {new Date(data!.scheduledAt).toLocaleString('es-VE', { dateStyle: 'medium', timeStyle: 'short' })}</>
            )}
          </p>
        </div>
        {error && <Badge tone="danger">⚠ Error al actualizar</Badge>}
      </div>

      {/* ── Controles de estado ──────────────────────────────────────────── */}
      <div className="mb-5">
        <SessionControls
          sessionId={sessionId}
          status={data!.status as SessionStatus}
          teamsCount={teams.length}
          onStateChange={load}
        />
      </div>

      {/* ── Código de acceso para participantes ──────────────────────────── */}
      {data!.accessCode && (
        <Card accent="brand" className="mb-5">
          <div className="flex items-center gap-4 flex-wrap">
            <div className="min-w-0">
              <div className="text-xs font-semibold uppercase tracking-wider text-brand-700">
                Código para participantes
              </div>
              <div className="text-3xl font-extrabold tracking-[0.35em] text-brand-700 font-mono mt-1">
                {data!.accessCode}
              </div>
            </div>
            <div className="ml-auto">
              <Button variant="secondary" onClick={copyCode}>
                {copied ? '✓ Copiado' : 'Copiar'}
              </Button>
            </div>
          </div>
        </Card>
      )}

      {/* ── Métricas ─────────────────────────────────────────────────────── */}
      <div className="flex flex-wrap gap-3 mb-5">
        <MetricCard
          label="Equipos registrados"
          value={teams.length}
          sub={teams.length === 0 ? 'Sin equipos registrados' : undefined}
        />
        <MetricCard label="Tiempo transcurrido" value={elapsed} sub="desde la creación de la sesión" />
        <MetricCard
          label="Estado"
          value={SESSION_STATUS_LABELS[data!.status as keyof typeof SESSION_STATUS_LABELS] ?? data!.status}
        />
      </div>

      <Stack gap={4}>
        {/* ── Progreso de equipos ──────────────────────────────────────── */}
        <Card>
          <CardHeader title="Progreso y acciones por equipo" />
          <TeamProgressPanel
            teams={teams}
            sessionId={sessionId}
            sessionStatus={data!.status}
            stages={stages}
            cluesByStage={cluesByStage}
            onClueReleased={load}
          />
        </Card>

        {/* ── Ranking en vivo (HU-21) ──────────────────────────────────── */}
        <Card>
          <CardHeader title="🏆 Ranking en vivo" />
          <SessionRankingPanel sessionId={sessionId} />
        </Card>

        {/* ── Eventos recientes (HU-9) ─────────────────────────────────── */}
        <Card>
          <CardHeader title="Eventos recientes" />
          {data!.recentEvents.length === 0 ? (
            <p className="text-sm text-ink-muted">Aún no hay eventos registrados para esta sesión.</p>
          ) : (
            <ul className="list-none m-0 p-0">
              {data!.recentEvents.map(ev => <EventRow key={ev.id} event={ev} />)}
            </ul>
          )}
        </Card>

        {/* ── Historial de auditoría (HU-22 + HU-26) ───────────────────── */}
        <Card>
          <CardHeader
            title="📜 Historial de auditoría"
            description="Línea de tiempo completa con quién, qué y cuándo. Útil para revisar reclamos o auditar la operación."
            actions={onOpenCommandAudit && (
              <Button
                variant="secondary"
                size="sm"
                onClick={onOpenCommandAudit}
                title="Abre la vista técnica con comandos CQRS, timestamps con milisegundos y exportación CSV (HU-26)"
              >
                🔍 Auditoría completa
              </Button>
            )}
          />
          <SessionAuditTimeline sessionId={sessionId} />
        </Card>
      </Stack>
    </div>
  );
}
