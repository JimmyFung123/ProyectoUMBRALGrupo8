import { useEffect, useRef, useState } from 'react';
import { sessionService } from '../../services/sessionService';
import { connectToSessionHub, type HubConnState } from '../../services/sessionHub';
import type { SessionRanking, SessionRankingTeam } from '../../types/ranking';
import { Badge, EmptyState, type BadgeTone } from '../ui';

const FALLBACK_POLL_MS = 10_000;
type ConnectionState = HubConnState;

interface Props {
  sessionId: string;
  /** Mayor orden de etapa de la misión. Un equipo con currentStageOrder por
   *  encima de este valor ya terminó la última etapa (finalizó). */
  totalStages?: number;
}

const RANK_COLOR: Record<number, string> = {
  1: '#f59e0b',
  2: '#94a3b8',
  3: '#b45309',
};

function RankBadge({ rank }: { rank: number }) {
  const medal = rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : null;
  return (
    <span
      className="font-bold inline-block min-w-[32px]"
      style={{ color: RANK_COLOR[rank] ?? '#475569' }}
    >
      {medal ?? `#${rank}`}
    </span>
  );
}

const CONN_TONE: Record<ConnectionState, BadgeTone> = {
  connecting:   'warning',
  connected:    'success',
  reconnecting: 'warning',
  disconnected: 'danger',
};

const CONN_LABEL: Record<ConnectionState, string> = {
  connecting:   'Conectando…',
  connected:    '● En vivo',
  reconnecting: 'Sincronizando…',
  disconnected: 'Desconectado',
};

function formatRelative(iso: string | null): string {
  if (!iso) return '—';
  const diffSec = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000));
  if (diffSec < 5)  return 'recién';
  if (diffSec < 60) return `hace ${diffSec}s`;
  const m = Math.floor(diffSec / 60);
  const s = diffSec % 60;
  return s === 0 ? `hace ${m}m` : `hace ${m}m ${s}s`;
}

function formatResolutionTime(iso: string | null): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleTimeString('es-VE', { timeStyle: 'medium' });
}

export function SessionRankingPanel({ sessionId, totalStages }: Props) {
  const [ranking, setRanking] = useState<SessionRanking | null>(null);
  const [lastSyncedAt, setLastSyncedAt] = useState<Date | null>(null);
  const [connState, setConnState] = useState<ConnectionState>('connecting');
  const [error, setError] = useState<string | null>(null);
  const [, setTick] = useState(0);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  async function loadRanking() {
    try {
      const data = await sessionService.getRanking(sessionId);
      setRanking(data);
      setLastSyncedAt(new Date());
      setError(null);
    } catch {
      setError('No se pudo sincronizar el ranking');
    }
  }

  useEffect(() => {
    const id = setInterval(() => setTick(t => t + 1), 1000);
    return () => clearInterval(id);
  }, []);

  useEffect(() => {
    const hub = connectToSessionHub({
      sessionId,
      onRefresh: () => { void loadRanking(); },
      onStateChange: setConnState,
    });
    void loadRanking();
    pollRef.current = setInterval(() => { void loadRanking(); }, FALLBACK_POLL_MS);
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
      hub.dispose();
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  const teams: SessionRankingTeam[] = ranking?.teams ?? [];

  return (
    <div>
      <div className="flex items-center gap-3 mb-3 flex-wrap">
        <Badge tone={CONN_TONE[connState]}>{CONN_LABEL[connState]}</Badge>
        <span className="text-xs text-ink-muted">
          Última sincronización: <strong>{formatRelative(lastSyncedAt?.toISOString() ?? null)}</strong>
          {lastSyncedAt && <> · {lastSyncedAt.toLocaleTimeString('es-VE', { timeStyle: 'medium' })}</>}
        </span>
        {error && <Badge tone="danger">⚠ {error}</Badge>}
      </div>

      {teams.length === 0 ? (
        <EmptyState
          icon="🏁"
          title="Aún no hay equipos en esta sesión"
          description="Cuando ingresen participantes verás el ranking actualizarse en vivo."
        />
      ) : (
        <div className="overflow-x-auto rounded border border-slate-200">
          <table className="w-full text-sm">
            <thead className="bg-surface-subtle">
              <tr>
                <Th>Pos.</Th>
                <Th>Equipo</Th>
                <Th align="center">Etapa</Th>
                <Th align="right">Puntos</Th>
                <Th align="center">Última resolución</Th>
              </tr>
            </thead>
            <tbody>
              {teams.map(team => (
                <tr
                  key={team.teamId}
                  className={`border-t border-slate-100 ${team.rank === 1 ? 'bg-warning-50/40' : ''}`}
                >
                  <Td><RankBadge rank={team.rank} /></Td>
                  <Td>
                    <span
                      title={team.isConnected ? 'Conectado' : 'Desconectado'}
                      className="inline-block w-2 h-2 rounded-full mr-2 align-middle"
                      style={{ background: team.isConnected ? '#16a34a' : '#cbd5e1' }}
                    />
                    <strong className="text-ink">{team.name}</strong>
                  </Td>
                  <Td align="center">
                    {team.currentStageOrder === 0
                      ? <span className="text-ink-subtle">—</span>
                      : (totalStages != null && totalStages > 0 && team.currentStageOrder > totalStages)
                        ? <span className="font-semibold" style={{ color: '#16a34a' }}>🏁 Finalizó</span>
                        : <span className="text-ink-soft">Etapa {team.currentStageOrder}</span>}
                  </Td>
                  <Td align="right">
                    <span className="font-bold tabular-nums text-ink">
                      {team.score.toLocaleString('es-VE')}
                    </span>
                  </Td>
                  <Td align="center">
                    <span className="text-xs text-ink-muted">
                      {formatResolutionTime(team.lastStageCompletedAt)}
                    </span>
                  </Td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

function Th({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'center' | 'right' }) {
  return (
    <th className={`px-3 py-2 text-xs font-semibold uppercase tracking-wider text-ink-muted text-${align}`}>
      {children}
    </th>
  );
}

function Td({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'center' | 'right' }) {
  return (
    <td className={`px-3 py-2.5 align-middle text-${align}`}>{children}</td>
  );
}
