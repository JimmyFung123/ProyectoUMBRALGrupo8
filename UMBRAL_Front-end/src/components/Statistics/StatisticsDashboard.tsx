import { useEffect, useMemo, useState } from 'react';
import { missionService } from '../../services/missionService';
import { statisticsService } from '../../services/statisticsService';
import type { Mission } from '../../types/mission';
import type {
  DashboardStatistics,
  StageEffectivenessStat,
  StageTimeStat,
} from '../../types/statistics';
import {
  Alert,
  Card,
  CardHeader,
  EmptyState,
  FormField,
  PageHeader,
  Select,
  Spinner,
  Stack,
} from '../ui';

/**
 * HU-25 — admin statistics dashboard.
 *
 * Renders two parallel bar charts (time per stage, effectiveness per stage)
 * built with pure CSS — no chart library needed, the design stays light.
 * The mission filter is loaded once and the dashboard is fetched again
 * whenever the user changes it.
 */
export function StatisticsDashboard() {
  const [missions, setMissions] = useState<Mission[]>([]);
  const [missionFilter, setMissionFilter] = useState<string>(''); // '' = todas
  const [data, setData] = useState<DashboardStatistics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    missionService.getAll().then(setMissions).catch(() => { /* dropdown stays empty */ });
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);
    statisticsService
      .getDashboard(missionFilter || null)
      .then(result => { if (!cancelled) setData(result); })
      .catch(err => { if (!cancelled) setError(err?.message ?? 'No se pudo cargar el dashboard.'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [missionFilter]);

  const hasData = useMemo(() => {
    if (!data) return false;
    return data.averageTimePerStage.length > 0 || data.effectivenessPerStage.length > 0;
  }, [data]);

  const generated = data?.generatedAt
    ? new Date(data.generatedAt).toLocaleString('es-VE', { dateStyle: 'short', timeStyle: 'medium' })
    : null;

  return (
    <div>
      <PageHeader
        eyebrow="Analítica"
        title="📊 Dashboard estadístico"
        description="Métricas históricas de sesiones finalizadas. Las sesiones activas o pausadas no entran en estas cifras hasta que cierran oficialmente."
      />

      <Card className="mb-4">
        <div className="flex items-end gap-3 flex-wrap">
          <div className="min-w-[260px]">
            <FormField label="Misión" htmlFor="stats-mission">
              <Select
                id="stats-mission"
                value={missionFilter}
                onChange={e => setMissionFilter(e.target.value)}
              >
                <option value="">— Todas las misiones —</option>
                {missions.map(m => <option key={m.id} value={m.id}>{m.name}</option>)}
              </Select>
            </FormField>
          </div>
          {generated && (
            <span className="ml-auto text-xs text-ink-muted">Actualizado el {generated}</span>
          )}
        </div>
      </Card>

      {loading && <Card><Spinner label="Cargando estadísticas…" /></Card>}
      {error && <Alert tone="danger">{`Error al cargar el dashboard: ${error}`}</Alert>}

      {!loading && !error && !hasData && (
        <Card>
          <EmptyState
            icon="📊"
            title={missionFilter ? 'Sin datos para esta misión' : 'Aún no hay sesiones finalizadas'}
            description={
              missionFilter
                ? 'No hay sesiones finalizadas para esta misión todavía.'
                : 'El dashboard se llenará a medida que las partidas se cierren.'
            }
          />
        </Card>
      )}

      {!loading && !error && hasData && data && (
        <div className="grid grid-cols-1 xl:grid-cols-2 gap-4">
          <TimePerStageCard rows={data.averageTimePerStage} />
          <EffectivenessPerStageCard rows={data.effectivenessPerStage} />
        </div>
      )}
    </div>
  );
}

// ── Time per stage ───────────────────────────────────────────────────────────

function TimePerStageCard({ rows }: { rows: StageTimeStat[] }) {
  const maxSeconds = Math.max(...rows.map(r => r.averageSeconds), 1);
  return (
    <Card>
      <CardHeader
        title="⏱ Tiempo promedio por etapa"
        description="Segundos que tomó a los equipos completar cada etapa, promediados sobre todas las sesiones finalizadas. Avances forzados no entran."
      />
      <Stack gap={2} className="pt-1">
        {rows.map(row => (
          <RowMetric
            key={row.stageOrder}
            label={`Etapa #${row.stageOrder}`}
            value={formatDuration(row.averageSeconds)}
            sub={`Muestra: ${row.sampleSize}`}
            barRatio={row.averageSeconds / maxSeconds}
            barColor="#6366f1"
            barLabel={`${Math.round(row.averageSeconds)}s`}
          />
        ))}
      </Stack>
    </Card>
  );
}

// ── Effectiveness per stage ──────────────────────────────────────────────────

function EffectivenessPerStageCard({ rows }: { rows: StageEffectivenessStat[] }) {
  if (rows.length === 0) {
    return (
      <Card>
        <CardHeader
          title="🎯 Efectividad de respuestas"
          description="No hay etapas de trivia finalizadas todavía — la efectividad se calcula únicamente para preguntas de opción múltiple."
        />
      </Card>
    );
  }
  return (
    <Card>
      <CardHeader
        title="🎯 Efectividad de respuestas"
        description="Porcentaje de respuestas correctas por etapa de trivia. La Búsqueda del Tesoro no aparece aquí porque el escaneo de QR no tiene 'respuesta incorrecta' — el equipo simplemente sigue intentando."
      />
      <Stack gap={2} className="pt-1">
        {rows.map(row => {
          const color = effectivenessColor(row.correctPercentage);
          return (
            <RowMetric
              key={row.stageOrder}
              label={`Etapa #${row.stageOrder}`}
              value={`${row.correctPercentage.toFixed(2)}%`}
              valueColor={color}
              sub={`${row.correctCount} de ${row.totalAnswers}`}
              barRatio={row.correctPercentage / 100}
              barColor={color}
              barLabel={`${row.correctPercentage.toFixed(0)}%`}
            />
          );
        })}
      </Stack>
    </Card>
  );
}

// ── Building blocks ──────────────────────────────────────────────────────────

interface RowMetricProps {
  label: string;
  value: string;
  valueColor?: string;
  sub?: string;
  barRatio: number;
  barColor: string;
  barLabel: string;
}

function RowMetric({ label, value, valueColor, sub, barRatio, barColor, barLabel }: RowMetricProps) {
  return (
    <div className="grid grid-cols-[160px_1fr] gap-3 items-center">
      <div className="min-w-0">
        <div className="text-sm font-semibold text-ink truncate">{label}</div>
        {sub && <div className="text-xs text-ink-muted">{sub}</div>}
      </div>
      <div className="flex items-center gap-3">
        <Bar ratio={barRatio} color={barColor} label={barLabel} />
        <span
          className="text-sm font-semibold tabular-nums shrink-0"
          style={valueColor ? { color: valueColor } : undefined}
        >
          {value}
        </span>
      </div>
    </div>
  );
}

function Bar({ ratio, color, label }: { ratio: number; color: string; label: string }) {
  const widthPct = Math.max(0, Math.min(1, ratio)) * 100;
  return (
    <div className="relative flex-1 bg-slate-100 h-5 rounded overflow-hidden min-w-[100px]">
      <div
        style={{ width: `${widthPct}%`, background: color }}
        className="h-full transition-all duration-200"
      />
      <span className="absolute inset-0 flex items-center justify-center text-[0.7rem] font-semibold text-white drop-shadow-sm">
        {label}
      </span>
    </div>
  );
}

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${Math.round(seconds)} s`;
  const mins = Math.floor(seconds / 60);
  const remainder = Math.round(seconds % 60);
  return remainder === 0 ? `${mins} min` : `${mins} min ${remainder} s`;
}

function effectivenessColor(percentage: number): string {
  if (percentage >= 75) return '#16a34a'; // success-600
  if (percentage >= 50) return '#d97706'; // warning-600
  return '#dc2626'; // danger-600
}
