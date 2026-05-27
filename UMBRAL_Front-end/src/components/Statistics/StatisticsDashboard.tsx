import { useEffect, useMemo, useState } from 'react';
import { missionService } from '../../services/missionService';
import { statisticsService } from '../../services/statisticsService';
import type { Mission } from '../../types/mission';
import type {
  DashboardStatistics,
  StageEffectivenessStat,
  StageTimeStat,
} from '../../types/statistics';

/**
 * HU-25 — admin statistics dashboard.
 *
 * Renders two parallel bar charts (time per stage, effectiveness per stage)
 * built with pure CSS — no chart library needed, the design stays light.
 * The mission filter is loaded once and the dashboard is fetched again
 * whenever the user changes it.
 *
 * The empty state (zero finalized sessions matching the filter) is shown
 * explicitly so an admin doesn't confuse "nothing recorded yet" with a
 * broken endpoint.
 */

const COLORS = {
  bg: '#f8fafc',
  cardBg: '#ffffff',
  border: '#e2e8f0',
  text: '#1e293b',
  muted: '#64748b',
  primary: '#6366f1',
  success: '#16a34a',
  warning: '#d97706',
  danger: '#dc2626',
};

export function StatisticsDashboard() {
  const [missions, setMissions] = useState<Mission[]>([]);
  const [missionFilter, setMissionFilter] = useState<string>(''); // '' = todas
  const [data, setData] = useState<DashboardStatistics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Cargar misiones para el dropdown (una sola vez).
  useEffect(() => {
    missionService
      .getAll()
      .then(setMissions)
      .catch(() => {
        // No bloqueamos el dashboard si fallan las misiones — el filtro
        // simplemente no se podrá usar.
      });
  }, []);

  // Cargar estadísticas cuando cambia el filtro.
  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    statisticsService
      .getDashboard(missionFilter || null)
      .then(result => {
        if (!cancelled) setData(result);
      })
      .catch(err => {
        if (!cancelled) setError(err?.message ?? 'No se pudo cargar el dashboard.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [missionFilter]);

  const hasData = useMemo(() => {
    if (!data) return false;
    return data.averageTimePerStage.length > 0 || data.effectivenessPerStage.length > 0;
  }, [data]);

  return (
    <div style={{ padding: '1.5rem', background: COLORS.bg, minHeight: '80vh' }}>
      <header style={{ marginBottom: '1.5rem' }}>
        <h1 style={{ margin: 0, color: COLORS.text, fontSize: '1.5rem' }}>
          📊 Dashboard estadístico
        </h1>
        <p style={{ margin: '0.25rem 0 0', color: COLORS.muted, fontSize: '0.875rem' }}>
          Métricas históricas de sesiones finalizadas. Las sesiones activas o
          pausadas no entran en estas cifras hasta que cierran oficialmente.
        </p>
      </header>

      <FilterBar
        missions={missions}
        value={missionFilter}
        onChange={setMissionFilter}
        generatedAt={data?.generatedAt}
      />

      {loading && <PlaceholderCard text="Cargando estadísticas..." />}

      {error && (
        <PlaceholderCard
          text={`Error al cargar el dashboard: ${error}`}
          tone="danger"
        />
      )}

      {!loading && !error && !hasData && (
        <PlaceholderCard
          text={
            missionFilter
              ? 'No hay sesiones finalizadas para esta misión todavía.'
              : 'Aún no se han finalizado sesiones — el dashboard se llenará a medida que las partidas se cierren.'
          }
        />
      )}

      {!loading && !error && hasData && data && (
        <div style={{ display: 'grid', gap: '1.5rem', gridTemplateColumns: 'repeat(auto-fit, minmax(420px, 1fr))' }}>
          <TimePerStageCard rows={data.averageTimePerStage} />
          <EffectivenessPerStageCard rows={data.effectivenessPerStage} />
        </div>
      )}
    </div>
  );
}

// ── FilterBar ────────────────────────────────────────────────────────────────

function FilterBar({
  missions,
  value,
  onChange,
  generatedAt,
}: {
  missions: Mission[];
  value: string;
  onChange: (id: string) => void;
  generatedAt?: string;
}) {
  const generated = generatedAt
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
      <label style={{ fontSize: '0.875rem', color: COLORS.text, fontWeight: 600 }}>
        Misión:&nbsp;
        <select
          value={value}
          onChange={e => onChange(e.target.value)}
          style={{
            padding: '0.35rem 0.5rem',
            border: `1px solid ${COLORS.border}`,
            borderRadius: 4,
            background: '#fff',
            fontSize: '0.875rem',
          }}
        >
          <option value="">— Todas las misiones —</option>
          {missions.map(m => (
            <option key={m.id} value={m.id}>
              {m.name}
            </option>
          ))}
        </select>
      </label>

      {generated && (
        <span style={{ marginLeft: 'auto', fontSize: '0.75rem', color: COLORS.muted }}>
          Actualizado el {generated}
        </span>
      )}
    </div>
  );
}

// ── Time per stage ───────────────────────────────────────────────────────────

function TimePerStageCard({ rows }: { rows: StageTimeStat[] }) {
  const maxSeconds = Math.max(...rows.map(r => r.averageSeconds), 1);

  return (
    <section style={cardStyle}>
      <h2 style={cardTitleStyle}>⏱ Tiempo promedio por etapa</h2>
      <p style={cardSubtitleStyle}>
        Segundos que tomó a los equipos completar cada etapa, promediados sobre
        todas las sesiones finalizadas. Avances forzados no entran.
      </p>
      <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '0.5rem' }}>
        <thead>
          <tr>
            <th style={thStyle}>Etapa</th>
            <th style={thStyle}>Promedio</th>
            <th style={thStyle}>Muestra</th>
            <th style={{ ...thStyle, width: '50%' }}>Distribución</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(row => (
            <tr key={row.stageOrder}>
              <td style={tdStyle}>#{row.stageOrder}</td>
              <td style={{ ...tdStyle, fontVariantNumeric: 'tabular-nums' }}>
                {formatDuration(row.averageSeconds)}
              </td>
              <td style={{ ...tdStyle, color: COLORS.muted }}>{row.sampleSize}</td>
              <td style={tdStyle}>
                <Bar
                  ratio={row.averageSeconds / maxSeconds}
                  color={COLORS.primary}
                  label={`${Math.round(row.averageSeconds)}s`}
                />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  );
}

// ── Effectiveness per stage ──────────────────────────────────────────────────

function EffectivenessPerStageCard({ rows }: { rows: StageEffectivenessStat[] }) {
  if (rows.length === 0) {
    return (
      <section style={cardStyle}>
        <h2 style={cardTitleStyle}>🎯 Efectividad de respuestas</h2>
        <p style={cardSubtitleStyle}>
          No hay etapas de trivia finalizadas todavía — la efectividad se calcula
          únicamente para preguntas de opción múltiple.
        </p>
      </section>
    );
  }

  return (
    <section style={cardStyle}>
      <h2 style={cardTitleStyle}>🎯 Efectividad de respuestas</h2>
      <p style={cardSubtitleStyle}>
        Porcentaje de respuestas correctas por etapa de trivia. La Búsqueda del
        Tesoro no aparece aquí porque el escaneo de QR no tiene "respuesta
        incorrecta" — el equipo simplemente sigue intentando.
      </p>
      <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '0.5rem' }}>
        <thead>
          <tr>
            <th style={thStyle}>Etapa</th>
            <th style={thStyle}>% Acierto</th>
            <th style={thStyle}>Correctas</th>
            <th style={thStyle}>Total</th>
            <th style={{ ...thStyle, width: '40%' }}>Distribución</th>
          </tr>
        </thead>
        <tbody>
          {rows.map(row => {
            const color = effectivenessColor(row.correctPercentage);
            return (
              <tr key={row.stageOrder}>
                <td style={tdStyle}>#{row.stageOrder}</td>
                <td style={{ ...tdStyle, color, fontWeight: 600, fontVariantNumeric: 'tabular-nums' }}>
                  {row.correctPercentage.toFixed(2)}%
                </td>
                <td style={tdStyle}>{row.correctCount}</td>
                <td style={{ ...tdStyle, color: COLORS.muted }}>{row.totalAnswers}</td>
                <td style={tdStyle}>
                  <Bar
                    ratio={row.correctPercentage / 100}
                    color={color}
                    label={`${row.correctPercentage.toFixed(0)}%`}
                  />
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </section>
  );
}

// ── Building blocks ──────────────────────────────────────────────────────────

function Bar({ ratio, color, label }: { ratio: number; color: string; label: string }) {
  const widthPct = Math.max(0, Math.min(1, ratio)) * 100;
  return (
    <div
      style={{
        position: 'relative',
        background: '#f1f5f9',
        height: 18,
        borderRadius: 4,
        overflow: 'hidden',
      }}
    >
      <div
        style={{
          width: `${widthPct}%`,
          background: color,
          height: '100%',
          transition: 'width 200ms ease',
        }}
      />
      <span
        style={{
          position: 'absolute',
          inset: 0,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          fontSize: '0.7rem',
          color: '#fff',
          fontWeight: 600,
          textShadow: '0 0 2px rgba(0,0,0,0.4)',
        }}
      >
        {label}
      </span>
    </div>
  );
}

function PlaceholderCard({ text, tone = 'muted' }: { text: string; tone?: 'muted' | 'danger' }) {
  return (
    <div
      style={{
        background: COLORS.cardBg,
        border: `1px solid ${tone === 'danger' ? COLORS.danger : COLORS.border}`,
        borderRadius: 8,
        padding: '2rem',
        textAlign: 'center',
        color: tone === 'danger' ? COLORS.danger : COLORS.muted,
      }}
    >
      {text}
    </div>
  );
}

// ── Helpers ──────────────────────────────────────────────────────────────────

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${Math.round(seconds)} s`;
  const mins = Math.floor(seconds / 60);
  const remainder = Math.round(seconds % 60);
  return remainder === 0 ? `${mins} min` : `${mins} min ${remainder} s`;
}

function effectivenessColor(percentage: number): string {
  if (percentage >= 75) return COLORS.success;
  if (percentage >= 50) return COLORS.warning;
  return COLORS.danger;
}

// ── Styles shared by both cards ──────────────────────────────────────────────

const cardStyle: React.CSSProperties = {
  background: COLORS.cardBg,
  border: `1px solid ${COLORS.border}`,
  borderRadius: 8,
  padding: '1.25rem',
};

const cardTitleStyle: React.CSSProperties = {
  margin: '0 0 0.25rem',
  color: COLORS.text,
  fontSize: '1.1rem',
};

const cardSubtitleStyle: React.CSSProperties = {
  margin: '0 0 0.75rem',
  color: COLORS.muted,
  fontSize: '0.8rem',
};

const thStyle: React.CSSProperties = {
  textAlign: 'left',
  fontSize: '0.75rem',
  color: COLORS.muted,
  textTransform: 'uppercase',
  letterSpacing: '0.04em',
  borderBottom: `1px solid ${COLORS.border}`,
  padding: '0.4rem 0.5rem',
};

const tdStyle: React.CSSProperties = {
  padding: '0.5rem',
  borderBottom: `1px solid ${COLORS.border}`,
  fontSize: '0.875rem',
};
