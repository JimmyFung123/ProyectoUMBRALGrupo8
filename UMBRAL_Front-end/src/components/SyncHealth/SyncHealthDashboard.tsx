import { useCallback, useEffect, useRef, useState } from 'react';
import { syncHealthService } from '../../services/syncHealthService';
import type {
  ProjectionHealth,
  RankingProjectionSession,
  ReprojectActionResult,
  SyncHealthSnapshot,
  SyncHealthStatus,
} from '../../types/syncHealth';

/**
 * HU-27 — admin-only sync-health dashboard.
 *
 * Polls /api/sync-health every 8 s and renders one card per CQRS read model.
 * Status colour comes directly from the backend classification (Healthy /
 * Warning / Critical). The ranking-projection card shows a per-session
 * dropdown so the admin can pick the exact session to reproject.
 *
 * The detection logic and re-projection actions live entirely server-side;
 * this component is just a polling read view + action buttons.
 */

const COLORS = {
  bg: '#f8fafc',
  cardBg: '#ffffff',
  border: '#e2e8f0',
  text: '#1e293b',
  muted: '#64748b',
  primary: '#6366f1',
  healthy: '#16a34a',
  warning: '#d97706',
  critical: '#dc2626',
};

const POLL_INTERVAL_MS = 8_000;

/**
 * Plain-language description for each projection card. Lives on the front
 * because it's purely explanatory — the backend only returns dynamic state
 * (counts, lag, drift). Update this map when a new projection is added to
 * `GetSyncHealthQueryHandler`.
 */
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
    return () => {
      if (pollHandle.current !== null) window.clearInterval(pollHandle.current);
    };
  }, [fetchSnapshot]);

  async function runAction(projection: ProjectionHealth, sessionId?: string) {
    const key = sessionId ? `${projection.projectionId}:${sessionId}` : projection.projectionId;
    setActionInProgress(key);
    setActionFeedback(null);
    try {
      const result = await syncHealthService.reproject(projection, sessionId);
      setActionFeedback(result);
      // Refresh state immediately so the card colours update without waiting
      // for the next poll tick.
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

  return (
    <div style={{ padding: '1.5rem', background: COLORS.bg, minHeight: '80vh' }}>
      <header style={{ marginBottom: '1.5rem' }}>
        <h1 style={{ margin: 0, color: COLORS.text, fontSize: '1.5rem' }}>
          🔄 Sincronización CQRS
        </h1>
        <p style={{ margin: '0.25rem 0 0', color: COLORS.muted, fontSize: '0.875rem' }}>
          Monitorea cada modelo de lectura del sistema y permite forzar una
          re-proyección cuando se detecta drift. Refresca cada 8 segundos.
        </p>
      </header>

      <ExplanationBanner />
      <StatusLegend generatedAt={snapshot?.generatedAt} />

      {actionFeedback && (
        <ActionFeedbackBanner
          result={actionFeedback}
          onDismiss={() => setActionFeedback(null)}
        />
      )}

      {loading && <PlaceholderCard text="Cargando estado de sincronización..." />}

      {error && !snapshot && (
        <PlaceholderCard text={`Error: ${error}`} tone="danger" />
      )}

      {snapshot && (
        <div
          style={{
            display: 'grid',
            gap: '1rem',
            gridTemplateColumns: 'repeat(auto-fit, minmax(420px, 1fr))',
          }}
        >
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

// ── ExplanationBanner ─────────────────────────────────────────────────────────

function ExplanationBanner() {
  return (
    <div
      style={{
        background: '#eef2ff',
        border: `1px solid ${COLORS.primary}33`,
        borderRadius: 8,
        padding: '0.75rem 1rem',
        marginBottom: '1rem',
        fontSize: '0.8rem',
        color: COLORS.text,
        lineHeight: 1.5,
      }}
    >
      <strong>ℹ️ ¿Cómo leer este panel?</strong>{' '}
      Una proyección está Healthy mientras el conteo de origen y réplica
      coincidan. El <em>Lag</em> que ves al lado solo mide cuánto pasó desde el
      último evento de dominio — si nadie creó/modificó datos en un buen rato,
      el lag crece y eso es normal. Solo el drift de conteos prueba que algo se
      perdió en RabbitMQ.
    </div>
  );
}

// ── StatusLegend ──────────────────────────────────────────────────────────────

function StatusLegend({ generatedAt }: { generatedAt?: string }) {
  const formattedTime = generatedAt
    ? new Date(generatedAt).toLocaleString('es-VE', {
        dateStyle: 'short',
        timeStyle: 'medium',
      })
    : null;
  return (
    <div
      style={{
        background: COLORS.cardBg,
        border: `1px solid ${COLORS.border}`,
        borderRadius: 8,
        padding: '0.75rem 1rem',
        marginBottom: '1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '1rem',
        flexWrap: 'wrap',
      }}
    >
      <strong style={{ color: COLORS.text, fontSize: '0.875rem' }}>Estado:</strong>
      <Legend tone="healthy" label="Healthy" hint="counts en sync" />
      <Legend tone="critical" label="Critical" hint="drift en counts" />
      {formattedTime && (
        <span style={{ marginLeft: 'auto', fontSize: '0.75rem', color: COLORS.muted }}>
          Última actualización: {formattedTime}
        </span>
      )}
    </div>
  );
}

function Legend({
  tone,
  label,
  hint,
}: {
  tone: 'healthy' | 'warning' | 'critical';
  label: string;
  hint: string;
}) {
  const color = tone === 'healthy' ? COLORS.healthy : tone === 'warning' ? COLORS.warning : COLORS.critical;
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', fontSize: '0.8rem', color: COLORS.text }}>
      <span
        style={{
          display: 'inline-block',
          width: 10,
          height: 10,
          borderRadius: '50%',
          background: color,
        }}
      />
      <strong>{label}</strong>
      <span style={{ color: COLORS.muted }}>· {hint}</span>
    </span>
  );
}

// ── ActionFeedbackBanner ──────────────────────────────────────────────────────

function ActionFeedbackBanner({
  result,
  onDismiss,
}: {
  result: ReprojectActionResult;
  onDismiss: () => void;
}) {
  const tone = result.success ? COLORS.healthy : COLORS.critical;
  return (
    <div
      style={{
        background: `${tone}11`,
        border: `1px solid ${tone}`,
        color: COLORS.text,
        borderRadius: 8,
        padding: '0.75rem 1rem',
        marginBottom: '1rem',
        display: 'flex',
        alignItems: 'center',
        gap: '1rem',
        flexWrap: 'wrap',
      }}
    >
      <span style={{ fontSize: '1.1rem' }}>{result.success ? '✅' : '⚠️'}</span>
      <span style={{ fontWeight: 600, color: tone }}>{result.projectionId}</span>
      <span style={{ flex: 1, fontSize: '0.875rem' }}>{result.detail}</span>
      <button
        type="button"
        onClick={onDismiss}
        style={{
          background: 'transparent',
          border: 'none',
          fontSize: '1.1rem',
          cursor: 'pointer',
          color: COLORS.muted,
        }}
        aria-label="Cerrar"
      >
        ✕
      </button>
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
  const statusColor = colorForStatus(projection.status);
  const lastUpdated = projection.lastUpdatedAt
    ? new Date(projection.lastUpdatedAt).toLocaleString('es-VE', {
        dateStyle: 'short',
        timeStyle: 'medium',
      })
    : '—';

  const isPerSessionCard = projection.requiresSessionId && projection.sessions !== null;
  const cardActionKey = isPerSessionCard
    ? `${projection.projectionId}:${selectedSession}`
    : projection.projectionId;
  const isBusy = actionInProgress === cardActionKey;

  return (
    <article
      style={{
        background: COLORS.cardBg,
        border: `1px solid ${COLORS.border}`,
        borderLeft: `6px solid ${statusColor}`,
        borderRadius: 8,
        padding: '1rem',
        display: 'flex',
        flexDirection: 'column',
        gap: '0.75rem',
      }}
    >
      <header style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', gap: '1rem' }}>
        <h3 style={{ margin: 0, fontSize: '1rem', color: COLORS.text }}>{projection.displayName}</h3>
        <StatusBadge status={projection.status} />
      </header>

      {PROJECTION_DESCRIPTIONS[projection.projectionId] && (
        <p
          style={{
            margin: 0,
            fontSize: '0.8rem',
            color: COLORS.muted,
            lineHeight: 1.45,
            background: '#f1f5f9',
            border: `1px dashed ${COLORS.border}`,
            borderRadius: 6,
            padding: '0.5rem 0.65rem',
          }}
        >
          {PROJECTION_DESCRIPTIONS[projection.projectionId]}
        </p>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.5rem', fontSize: '0.85rem' }}>
        <Metric label="Servicio dueño" value={projection.owningService} />
        <Metric label="Última actualización" value={lastUpdated} />
        <Metric label="Modelo origen" value={projection.sourceModel} />
        <Metric label="Modelo lectura" value={projection.readModel} />
        <Metric label="Conteo origen" value={String(projection.sourceCount)} />
        <Metric
          label="Conteo réplica"
          value={String(projection.readCount)}
          highlight={projection.sourceCount !== projection.readCount ? COLORS.critical : undefined}
        />
        {projection.lagSeconds !== null && (
          <Metric
            label="Lag (informativo)"
            value={`${projection.lagSeconds}s`}
          />
        )}
      </div>

      <p style={{ margin: 0, fontSize: '0.85rem', color: COLORS.muted }}>{projection.detail}</p>

      {isPerSessionCard && projection.sessions && (
        <SessionPicker
          sessions={projection.sessions}
          selected={selectedSession}
          onChange={setSelectedSession}
        />
      )}

      {projection.supportsReproject && (
        <button
          type="button"
          disabled={isBusy || (isPerSessionCard && !selectedSession)}
          onClick={() =>
            onReproject(projection, isPerSessionCard ? selectedSession : undefined)
          }
          style={{
            alignSelf: 'flex-start',
            padding: '0.5rem 1rem',
            background: isBusy ? COLORS.muted : COLORS.primary,
            color: '#fff',
            border: 'none',
            borderRadius: 6,
            fontSize: '0.875rem',
            cursor: isBusy || (isPerSessionCard && !selectedSession) ? 'not-allowed' : 'pointer',
            opacity: isPerSessionCard && !selectedSession ? 0.6 : 1,
          }}
        >
          {isBusy
            ? 'Procesando…'
            : projection.projectionId === 'stage-completion-records'
              ? 'Reconciliar flag'
              : 'Reproyectar'}
        </button>
      )}
    </article>
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
      <p style={{ margin: 0, fontSize: '0.8rem', color: COLORS.muted, fontStyle: 'italic' }}>
        No hay sesiones con equipos o proyecciones activas.
      </p>
    );
  }
  return (
    <label style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem', fontSize: '0.85rem' }}>
      <span style={{ color: COLORS.text, fontWeight: 600 }}>Sesión a reproyectar</span>
      <select
        value={selected}
        onChange={e => onChange(e.target.value)}
        style={{
          padding: '0.35rem 0.5rem',
          border: `1px solid ${COLORS.border}`,
          borderRadius: 4,
          background: '#fff',
        }}
      >
        <option value="">— Selecciona una sesión —</option>
        {sessions.map(s => (
          <option key={s.sessionId} value={s.sessionId}>
            {labelForSession(s)}
          </option>
        ))}
      </select>
    </label>
  );
}

function labelForSession(s: RankingProjectionSession): string {
  const lag = s.lagSeconds !== null ? `lag ${s.lagSeconds}s` : 'sin lag';
  const drift = s.teamCount !== s.projectionCount ? ' ⚠ drift' : '';
  return `${s.sessionId.substring(0, 8)} (${s.sessionStatus}) · equipos ${s.teamCount}/proj ${s.projectionCount} · ${lag} · ${s.status}${drift}`;
}

// ── small UI helpers ──────────────────────────────────────────────────────────

function Metric({ label, value, highlight }: { label: string; value: string; highlight?: string }) {
  return (
    <div>
      <div style={{ color: COLORS.muted, fontSize: '0.7rem', textTransform: 'uppercase', letterSpacing: 0.4 }}>
        {label}
      </div>
      <div style={{ color: highlight ?? COLORS.text, fontWeight: 600 }}>{value}</div>
    </div>
  );
}

function StatusBadge({ status }: { status: SyncHealthStatus }) {
  const color = colorForStatus(status);
  return (
    <span
      style={{
        display: 'inline-block',
        background: color,
        color: '#fff',
        fontSize: '0.7rem',
        fontWeight: 700,
        padding: '0.25rem 0.6rem',
        borderRadius: 12,
        textTransform: 'uppercase',
        letterSpacing: 0.5,
      }}
    >
      {status}
    </span>
  );
}

function colorForStatus(status: SyncHealthStatus): string {
  switch (status) {
    case 'Healthy':
      return COLORS.healthy;
    case 'Warning':
      return COLORS.warning;
    case 'Critical':
      return COLORS.critical;
    default:
      return COLORS.muted;
  }
}

function PlaceholderCard({ text, tone }: { text: string; tone?: 'danger' }) {
  const accent = tone === 'danger' ? COLORS.critical : COLORS.muted;
  return (
    <div
      style={{
        background: COLORS.cardBg,
        border: `1px solid ${COLORS.border}`,
        borderLeft: `4px solid ${accent}`,
        borderRadius: 8,
        padding: '1.5rem',
        color: accent,
        fontSize: '0.9rem',
      }}
    >
      {text}
    </div>
  );
}
