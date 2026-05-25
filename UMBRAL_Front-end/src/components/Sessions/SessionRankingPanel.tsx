import * as signalR from '@microsoft/signalr';
import { useEffect, useRef, useState } from 'react';
import { sessionService } from '../../services/sessionService';
import type { SessionRanking, SessionRankingTeam } from '../../types/ranking';

const SIGNALR_URL = import.meta.env.VITE_SESSION_SIGNALR_URL ?? 'http://localhost:5092/hubs/session';

// Fallback HTTP poll period when SignalR is unavailable or while it reconnects.
// HU-21 expects "instantáneo" via WebSockets — polling is the safety net (AC #4).
const FALLBACK_POLL_MS = 10_000;

type ConnectionState = 'connecting' | 'connected' | 'reconnecting' | 'disconnected';

interface Props {
  sessionId: string;
}

const RANK_COLORS: Record<number, string> = {
  1: '#f9a825',
  2: '#90a4ae',
  3: '#a1887f',
};

function RankBadge({ rank }: { rank: number }) {
  const medal = rank === 1 ? '🥇' : rank === 2 ? '🥈' : rank === 3 ? '🥉' : null;
  return (
    <span style={{
      fontWeight: 'bold',
      color: RANK_COLORS[rank] ?? '#555',
      minWidth: 32,
      display: 'inline-block',
    }}>
      {medal ?? `#${rank}`}
    </span>
  );
}

function ConnectionPill({ state }: { state: ConnectionState }) {
  const config: Record<ConnectionState, { label: string; bg: string; color: string }> = {
    connecting:   { label: 'Conectando…',    bg: '#fff3cd', color: '#856404' },
    connected:    { label: '● En vivo',      bg: '#d4edda', color: '#155724' },
    reconnecting: { label: 'Sincronizando…', bg: '#fff3cd', color: '#856404' },
    disconnected: { label: 'Desconectado',   bg: '#f8d7da', color: '#721c24' },
  };
  const c = config[state];
  return (
    <span style={{
      fontSize: '0.75rem',
      padding: '0.15rem 0.55rem',
      borderRadius: 999,
      background: c.bg,
      color: c.color,
      fontWeight: 600,
    }}>
      {c.label}
    </span>
  );
}

/** Formats an ISO timestamp as "hace Xs / Xm". */
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

/**
 * HU-21 — Ranking en vivo.
 *
 * Listens to the SignalR "SessionStateChanged" event for instant updates,
 * falls back to a 10s HTTP poll if the hub is unreachable (criterion 4),
 * and always exposes the timestamp of the last successful sync (criterion 5).
 * Retains the last known ranking when the connection drops so the UI
 * never goes blank (flujo alterno HU-21).
 */
export function SessionRankingPanel({ sessionId }: Props) {
  const [ranking, setRanking] = useState<SessionRanking | null>(null);
  const [lastSyncedAt, setLastSyncedAt] = useState<Date | null>(null);
  const [connState, setConnState] = useState<ConnectionState>('connecting');
  const [error, setError] = useState<string | null>(null);
  const [, setTick] = useState(0); // re-render every second for "hace Xs"

  const hubRef = useRef<signalR.HubConnection | null>(null);
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

  // Tick every second so the "hace Xs" indicator stays fresh without re-fetching.
  useEffect(() => {
    const id = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(id);
  }, []);

  // SignalR + polling fallback
  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_URL)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('SessionStateChanged', () => { void loadRanking(); });

    connection.onreconnecting(() => setConnState('reconnecting'));
    connection.onreconnected(() => {
      setConnState('connected');
      // Re-join the group and resync state after reconnection.
      connection.invoke('JoinSession', sessionId).catch(() => { /* swallow */ });
      void loadRanking();
    });
    connection.onclose(() => setConnState('disconnected'));

    connection.start()
      .then(() => {
        setConnState('connected');
        return connection.invoke('JoinSession', sessionId);
      })
      .catch(() => setConnState('disconnected'));

    hubRef.current = connection;

    // Always run the HTTP fallback poll, regardless of SignalR state.
    void loadRanking();
    pollRef.current = setInterval(() => { void loadRanking(); }, FALLBACK_POLL_MS);

    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
      connection.invoke('LeaveSession', sessionId).catch(() => {});
      connection.stop().catch(() => {});
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  const teams: SessionRankingTeam[] = ranking?.teams ?? [];

  return (
    <div>
      <div style={{
        display: 'flex',
        alignItems: 'center',
        gap: '0.6rem',
        marginBottom: '0.75rem',
        flexWrap: 'wrap',
      }}>
        <ConnectionPill state={connState} />
        <span style={{ fontSize: '0.78rem', color: '#777' }}>
          Última sincronización: <strong>{formatRelative(lastSyncedAt?.toISOString() ?? null)}</strong>
          {lastSyncedAt && (
            <> · {lastSyncedAt.toLocaleTimeString('es-VE', { timeStyle: 'medium' })}</>
          )}
        </span>
        {error && (
          <span style={{ marginLeft: 'auto', fontSize: '0.78rem', color: '#c0392b' }}>
            ⚠ {error}
          </span>
        )}
      </div>

      {teams.length === 0 ? (
        <p style={{ color: '#999', fontSize: '0.9rem', margin: 0 }}>
          Aún no hay equipos en esta sesión.
        </p>
      ) : (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' }}>
            <thead>
              <tr style={{ background: '#f5f5f5', textAlign: 'left' }}>
                <th style={thStyle}>Pos.</th>
                <th style={thStyle}>Equipo</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>Etapa</th>
                <th style={{ ...thStyle, textAlign: 'right' }}>Puntos</th>
                <th style={{ ...thStyle, textAlign: 'center' }}>Última resolución</th>
              </tr>
            </thead>
            <tbody>
              {teams.map((team) => (
                <tr key={team.teamId} style={{
                  borderBottom: '1px solid #eee',
                  background: team.rank === 1 ? '#fffde7' : undefined,
                }}>
                  <td style={tdStyle}><RankBadge rank={team.rank} /></td>
                  <td style={tdStyle}>
                    <span
                      title={team.isConnected ? 'Conectado' : 'Desconectado'}
                      style={{
                        display: 'inline-block',
                        width: 8, height: 8, borderRadius: '50%',
                        background: team.isConnected ? '#27ae60' : '#bdc3c7',
                        marginRight: '0.4rem',
                      }}
                    />
                    <strong>{team.name}</strong>
                  </td>
                  <td style={{ ...tdStyle, textAlign: 'center', color: '#555' }}>
                    {team.currentStageOrder === 0
                      ? <span style={{ color: '#aaa' }}>—</span>
                      : `Etapa ${team.currentStageOrder}`}
                  </td>
                  <td style={{ ...tdStyle, textAlign: 'right', fontWeight: 'bold', color: '#2c3e50' }}>
                    {team.score.toLocaleString('es-VE')}
                  </td>
                  <td style={{ ...tdStyle, textAlign: 'center', color: '#777', fontSize: '0.82rem' }}>
                    {formatResolutionTime(team.lastStageCompletedAt)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

const thStyle: React.CSSProperties = {
  padding: '0.5rem 0.75rem',
  fontWeight: 600,
  fontSize: '0.8rem',
  color: '#555',
  borderBottom: '2px solid #ddd',
};

const tdStyle: React.CSSProperties = {
  padding: '0.55rem 0.75rem',
  verticalAlign: 'middle',
};
