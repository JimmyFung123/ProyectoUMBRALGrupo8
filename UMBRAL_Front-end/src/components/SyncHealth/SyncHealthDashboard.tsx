import { useCallback, useEffect, useRef, useState } from 'react';
import { syncHealthService } from '../../services/syncHealthService';
import type {
  ProjectionHealth,
  RankingProjectionSession,
  ReprojectActionResult,
  SyncHealthSnapshot,
  SyncHealthStatus,
} from '../../types/syncHealth';
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  EmptyState,
  PageHeader,
  Select,
  Spinner,
  type BadgeTone,
} from '../ui';

/**
 * HU-27 — admin-only sync-health dashboard.
 *
 * Polls /api/sync-health every 8 s and renders one card per CQRS read model.
 * Status colour comes directly from the backend classification (Healthy /
 * Warning / Critical). The ranking-projection card shows a per-session
 * dropdown so the admin can pick the exact session to reproject.
 */
const POLL_INTERVAL_MS = 8_000;

const PROJECTION_DESCRIPTIONS: Record<string, string> = {
  'missions-lookup-session':
    'Réplica local de las misiones que SessionService usa para validar que una sesión nueva se cree sobre una misión activa (RB-01). Se mantiene en sync vía RabbitMQ desde MissionService.',
  'missions-lookup-stage':
    'Réplica local de las misiones que StageService consulta al crear o editar etapas, para verificar que la misión madre exista y no esté bloqueada. Se actualiza vía RabbitMQ.',
  'stage-count-lookup':
    'Resumen "cuántas etapas tiene cada misión" usado por MissionService al activar una misión (RB-13). Lo actualizan los eventos StageAdded/StageRemoved que emite StageService.',
  'stage-lookup':
    'Catálogo de etapas que ClueService consulta al crear pistas para confirmar que la etapa destino existe y su misión coincide. Se alimenta vía RabbitMQ desde StageService.',
  'ranking-projection':
    'Modelo CQRS de lectura del ranking en vivo (HU-24). Cada sesión tiene su propia proyección pre-ordenada por puntaje y tiempo de resolución; los handlers de TeamService la reconstruyen en la misma transacción que cada cambio de puntaje.',
  'stage-completion-records':
    'Fact-table histórica del dashboard de estadísticas (HU-25). Cada vez que un equipo cambia de etapa se inserta una fila; al finalizar la sesión se activa el flag IncludedInStatistics para que aparezca en el dashboard de administrador.',
};

const STATUS_TONE: Record<SyncHealthStatus, BadgeTone> = {
  Healthy: 'success',
  Warning: 'warning',
  Critical: 'danger',
};

const STATUS_ACCENT: Record<SyncHealthStatus, 'success' | 'warning' | 'danger'> = {
  Healthy: 'success',
  Warning: 'warning',
  Critical: 'danger',
};

export function SyncHealthDashboard() {
  const [snapshot, setSnapshot] = useState<SyncHealthSnapshot | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionFeedback, setActionFeedback] = useState<ReprojectActionResult | null>(null);
  const [actionInProgress, setActionInProgress] = useState<string | null>(null);
  const pollHandle = useRef<number | null>(null);

  const fetchSnapshot = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    try {
      const data = await syncHealthService.getSnapshot();
      setSnapshot(data);
      setError(null);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'No se pudo cargar el estado de sincronización.';
      setError(message);
    } finally {
      if (!silent) setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchSnapshot(false);
    pollHandle.current = window.setInterval(() => fetchSnapshot(true), POLL_INTERVAL_MS);
    return () => { if (pollHandle.current !== null) window.clearInterval(pollHandle.current); };
  }, [fetchSnapshot]);

  async function runAction(projection: ProjectionHealth, sessionId?: string) {
    const key = sessionId ? `${projection.projectionId}:${sessionId}` : projection.projectionId;
    setActionInProgress(key);
    setActionFeedback(null);
    try {
      const result = await syncHealthService.reproject(projection, sessionId);
      setActionFeedback(result);
      await fetchSnapshot(true);
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Acción falló.';
      setActionFeedback({
        projectionId: projection.projectionId,
        success: false,
        changedRows: 0,
        detail: message,
        completedAt: new Date().toISOString(),
      });
    } finally {
      setActionInProgress(null);
    }
  }

  const generated = snapshot?.generatedAt
    ? new Date(snapshot.generatedAt).toLocaleString('es-VE', { dateStyle: 'short', timeStyle: 'medium' })
    : null;

  return (
    <div>
      <PageHeader
        eyebrow="Infraestructura"
        title="🔄 Sincronización CQRS"
        description="Monitorea cada modelo de lectura del sistema y permite forzar una re-proyección cuando se detecta drift. Refresca cada 8 segundos."
      />

      <Alert tone="info" className="mb-4" icon="ℹ️" title="¿Cómo leer este panel?">
        Una proyección está <strong>Healthy</strong> mientras el conteo de origen y réplica
        coincidan. El <em>Lag</em> que ves al lado solo mide cuánto pasó desde el
        último evento de dominio — si nadie creó/modificó datos en un buen rato,
        el lag crece y eso es normal. Solo el drift de conteos prueba que algo se
        perdió en RabbitMQ.
      </Alert>

      <Card className="mb-4">
        <div className="flex items-center gap-3 flex-wrap text-sm">
          <strong className="text-ink">Estado:</strong>
          <Badge tone="success">Healthy — counts en sync</Badge>
          <Badge tone="danger">Critical — drift en counts</Badge>
          {generated && (
            <span className="ml-auto text-xs text-ink-muted">Última actualización: {generated}</span>
          )}
        </div>
      </Card>

      {actionFeedback && (
        <Alert
          tone={actionFeedback.success ? 'success' : 'danger'}
          title={actionFeedback.projectionId}
          onDismiss={() => setActionFeedback(null)}
          className="mb-4"
        >
          {actionFeedback.detail}
        </Alert>
      )}

      {loading && <Card><Spinner label="Cargando estado de sincronización…" /></Card>}
      {error && !snapshot && <Alert tone="danger">{error}</Alert>}

      {snapshot && (
        <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
          {snapshot.projections.map(p => (
            <ProjectionCard
              key={p.projectionId}
              projection={p}
              actionInProgress={actionInProgress}
              onReproject={runAction}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// ── ProjectionCard ────────────────────────────────────────────────────────────

function ProjectionCard({
  projection,
  actionInProgress,
  onReproject,
}: {
  projection: ProjectionHealth;
  actionInProgress: string | null;
  onReproject: (projection: ProjectionHealth, sessionId?: string) => void;
}) {
  const [selectedSession, setSelectedSession] = useState<string>('');
  const lastUpdated = projection.lastUpdatedAt
    ? new Date(projection.lastUpdatedAt).toLocaleString('es-VE', { dateStyle: 'short', timeStyle: 'medium' })
    : '—';

  const isPerSessionCard = projection.requiresSessionId && projection.sessions !== null;
  const cardActionKey = isPerSessionCard
    ? `${projection.projectionId}:${selectedSession}`
    : projection.projectionId;
  const isBusy = actionInProgress === cardActionKey;
  const accent = STATUS_ACCENT[projection.status];
  const description = PROJECTION_DESCRIPTIONS[projection.projectionId];

  return (
    <Card accent={accent}>
      <CardHeader
        title={projection.displayName}
        actions={<Badge tone={STATUS_TONE[projection.status]} variant="solid">{projection.status}</Badge>}
      />

      {description && (
        <p className="text-xs text-ink-muted bg-surface-inset border border-dashed border-slate-200 rounded px-3 py-2 leading-snug mb-3">
          {description}
        </p>
      )}

      <div className="grid grid-cols-2 gap-y-2 gap-x-3 text-sm">
        <Metric label="Servicio dueño" value={projection.owningService} />
        <Metric label="Última actualización" value={lastUpdated} />
        <Metric label="Modelo origen" value={projection.sourceModel} />
        <Metric label="Modelo lectura" value={projection.readModel} />
        <Metric label="Conteo origen" value={String(projection.sourceCount)} />
        <Metric
          label="Conteo réplica"
          value={String(projection.readCount)}
          highlight={projection.sourceCount !== projection.readCount}
        />
        {projection.lagSeconds !== null && (
          <Metric label="Lag (informativo)" value={`${projection.lagSeconds}s`} />
        )}
      </div>

      <p className="text-sm text-ink-muted mt-3">{projection.detail}</p>

      {isPerSessionCard && projection.sessions && (
        <div className="mt-3">
          <SessionPicker
            sessions={projection.sessions}
            selected={selectedSession}
            onChange={setSelectedSession}
          />
        </div>
      )}

      {projection.supportsReproject && (
        <div className="mt-3">
          <Button
            size="sm"
            disabled={isBusy || (isPerSessionCard && !selectedSession)}
            onClick={() => onReproject(projection, isPerSessionCard ? selectedSession : undefined)}
          >
            {isBusy
              ? 'Procesando…'
              : projection.projectionId === 'stage-completion-records'
                ? 'Reconciliar flag'
                : 'Reproyectar'}
          </Button>
        </div>
      )}
    </Card>
  );
}

function SessionPicker({
  sessions,
  selected,
  onChange,
}: {
  sessions: RankingProjectionSession[];
  selected: string;
  onChange: (id: string) => void;
}) {
  if (sessions.length === 0) {
    return (
      <EmptyState
        icon="🛈"
        title="Sin sesiones monitoreadas"
        description="No hay sesiones con equipos o proyecciones activas todavía."
      />
    );
  }
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="font-semibold text-ink">Sesión a reproyectar</span>
      <Select value={selected} onChange={e => onChange(e.target.value)}>
        <option value="">— Selecciona una sesión —</option>
        {sessions.map(s => (
          <option key={s.sessionId} value={s.sessionId}>
            {labelForSession(s)}
          </option>
        ))}
      </Select>
    </label>
  );
}

function labelForSession(s: RankingProjectionSession): string {
  const lag = s.lagSeconds !== null ? `lag ${s.lagSeconds}s` : 'sin lag';
  const drift = s.teamCount !== s.projectionCount ? ' ⚠ drift' : '';
  return `${s.sessionId.substring(0, 8)} (${s.sessionStatus}) · equipos ${s.teamCount}/proj ${s.projectionCount} · ${lag} · ${s.status}${drift}`;
}

function Metric({ label, value, highlight }: { label: string; value: string; highlight?: boolean }) {
  return (
    <div>
      <div className="text-[0.7rem] uppercase tracking-wider text-ink-muted font-semibold">
        {label}
      </div>
      <div className={`font-semibold mt-0.5 ${highlight ? 'text-danger-600' : 'text-ink'}`}>
        {value}
      </div>
    </div>
  );
}
