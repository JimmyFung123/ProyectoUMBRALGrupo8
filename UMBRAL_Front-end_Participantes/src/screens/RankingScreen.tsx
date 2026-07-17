import { useCallback, useEffect, useRef, useState } from 'react';
import { getSessionRanking } from '../services/sessionService';
import type { SessionRanking, SessionRankingTeam } from '../types';

// Participant uses lightweight polling (HU-21 AC #4: fallback HTTP) — no SignalR client here.
// Matches the project convention noted in CLAUDE.md (polling every ~5s for participants).
const POLL_INTERVAL_MS = 5_000;

type ConnState = 'connecting' | 'connected' | 'disconnected';

interface Props {
  sessionId: string;
  teamId: string;
  onBack: () => void;
}

function formatRelative(date: Date | null): string {
  if (!date) return '—';
  const diffSec = Math.max(0, Math.floor((Date.now() - date.getTime()) / 1000));
  if (diffSec < 5)  return 'recién';
  if (diffSec < 60) return `hace ${diffSec}s`;
  const m = Math.floor(diffSec / 60);
  return `hace ${m}m`;
}

function MedalOrPosition({ rank }: { rank: number }) {
  if (rank === 1) return <span style={styles.medal}>🥇</span>;
  if (rank === 2) return <span style={styles.medal}>🥈</span>;
  if (rank === 3) return <span style={styles.medal}>🥉</span>;
  return <span style={styles.position}>#{rank}</span>;
}

/**
 * HU-21 — Ranking en vivo (vista del participante).
 *
 * Pulls the ranking every 5s; retains last known data on error so the UI
 * doesn't blank out if the network drops mid-game (flujo alterno HU-21).
 * Highlights the participant's own team for quick orientation.
 */
export function RankingScreen({ sessionId, teamId, onBack }: Props) {
  const [ranking, setRanking] = useState<SessionRanking | null>(null);
  const [lastSyncedAt, setLastSyncedAt] = useState<Date | null>(null);
  const [connState, setConnState] = useState<ConnState>('connecting');
  const [, setTick] = useState(0);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadRanking = useCallback(async () => {
    try {
      const data = await getSessionRanking(sessionId);
      setRanking(data);
      setLastSyncedAt(new Date());
      setConnState('connected');
    } catch {
      setConnState('disconnected');
      // Retain last known ranking on the screen.
    }
  }, [sessionId]);

  useEffect(() => {
    void loadRanking();
    intervalRef.current = setInterval(() => { void loadRanking(); }, POLL_INTERVAL_MS);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [loadRanking]);

  // Tick once per second to refresh the "hace Xs" indicator.
  useEffect(() => {
    const id = setInterval(() => setTick((t) => t + 1), 1000);
    return () => clearInterval(id);
  }, []);

  const teams: SessionRankingTeam[] = ranking?.teams ?? [];
  const myTeam = teams.find((t) => t.teamId === teamId);

  const connColor =
    connState === 'connected'    ? '#34d399' :
    connState === 'disconnected' ? '#f87171' :
                                   '#fbbf24';
  const connLabel =
    connState === 'connected'    ? 'En vivo' :
    connState === 'disconnected' ? 'Reconectando…' :
                                   'Conectando…';

  return (
    <div style={styles.container}>
      <div style={styles.header}>
        <button onClick={onBack} style={styles.backButton} aria-label="Volver">
          ←
        </button>
        <h1 style={styles.title}>🏆 Ranking</h1>
        <div style={styles.connIndicator}>
          <span style={{ ...styles.dot, background: connColor }} />
          <span style={styles.connLabel}>{connLabel}</span>
        </div>
      </div>

      <p style={styles.subtitle}>
        Actualización: <strong>{formatRelative(lastSyncedAt)}</strong>
      </p>

      {ranking?.sessionStatus === 'Pending' && (
        <div style={styles.notice}>
          La sesión aún no ha iniciado — todos los equipos están en 0 puntos.
        </div>
      )}

      {teams.length === 0 ? (
        <div style={styles.empty}>
          Aún no hay equipos registrados en esta sesión.
        </div>
      ) : (
        <ul style={styles.list}>
          {teams.map((team) => {
            const isMine = team.teamId === teamId;
            return (
              <li
                key={team.teamId}
                style={{
                  ...styles.row,
                  ...(isMine ? styles.rowMine : {}),
                  ...(team.rank === 1 ? styles.rowFirst : {}),
                }}
              >
                <div style={styles.rankCol}>
                  <MedalOrPosition rank={team.rank} />
                </div>
                <div style={styles.teamCol}>
                  <div style={styles.teamName}>
                    {team.name}
                    {isMine && <span style={styles.youTag}>tú</span>}
                  </div>
                  <div style={styles.teamMeta}>
                    {team.currentStageOrder === 0
                      ? 'Esperando…'
                      : `Etapa ${team.currentStageOrder}`}
                  </div>
                </div>
                <div style={styles.scoreCol}>
                  <div style={styles.score}>{team.score.toLocaleString('es-VE')}</div>
                  <div style={styles.scoreLabel}>pts</div>
                </div>
              </li>
            );
          })}
        </ul>
      )}

      {myTeam && (
        <div style={styles.myTeamSummary}>
          <span>Tu equipo: <strong>{myTeam.name}</strong></span>
          <span>Posición <strong>#{myTeam.rank}</strong> · {myTeam.score} pts</span>
        </div>
      )}
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    // Overlay full-screen: GameScreen stays mounted below so its clue state
    // and SignalR connection are not torn down when the user opens the ranking.
    // zIndex must beat Leaflet's internal layers (panes 200-700, controls 800-1000)
    // so the map tiles/markers do not bleed through on the TreasureHunt screen.
    position: 'fixed',
    inset: 0,
    overflowY: 'auto',
    zIndex: 2000,
    background: '#0f172a',
    color: '#f8fafc',
    padding: '1rem',
    paddingBottom: '5rem',
    boxSizing: 'border-box',
  },
  header: {
    display: 'flex',
    alignItems: 'center',
    gap: '0.75rem',
    marginBottom: '0.25rem',
  },
  backButton: {
    background: '#1e293b',
    color: '#f8fafc',
    border: 'none',
    borderRadius: 10,
    width: 40,
    height: 40,
    fontSize: '1.25rem',
    cursor: 'pointer',
  },
  title: { margin: 0, fontSize: '1.5rem', fontWeight: 800, flex: 1 },
  connIndicator: {
    display: 'flex',
    alignItems: 'center',
    gap: '0.35rem',
    background: '#1e293b',
    padding: '0.3rem 0.6rem',
    borderRadius: 999,
  },
  dot: {
    width: 8,
    height: 8,
    borderRadius: '50%',
    display: 'inline-block',
  },
  connLabel: { fontSize: '0.7rem', color: '#cbd5e1' },
  subtitle: {
    color: '#94a3b8',
    fontSize: '0.8rem',
    margin: '0 0 1rem',
  },
  notice: {
    background: '#1e293b',
    border: '1px solid #6366f1',
    color: '#c7d2fe',
    padding: '0.6rem 0.85rem',
    borderRadius: 8,
    fontSize: '0.85rem',
    marginBottom: '0.85rem',
  },
  empty: {
    background: '#1e293b',
    padding: '1.25rem',
    borderRadius: 12,
    textAlign: 'center',
    color: '#94a3b8',
    fontSize: '0.95rem',
  },
  list: {
    listStyle: 'none',
    padding: 0,
    margin: 0,
    display: 'flex',
    flexDirection: 'column',
    gap: '0.5rem',
  },
  row: {
    display: 'flex',
    alignItems: 'center',
    background: '#1e293b',
    padding: '0.7rem 0.85rem',
    borderRadius: 12,
    gap: '0.75rem',
  },
  rowMine: {
    background: '#312e81',
    boxShadow: '0 0 0 2px #6366f1',
  },
  rowFirst: {
    background: 'linear-gradient(135deg, #1e293b 0%, #422006 100%)',
  },
  rankCol: { width: 40, textAlign: 'center' },
  teamCol: { flex: 1, minWidth: 0 },
  teamName: {
    fontWeight: 700,
    fontSize: '1rem',
    color: '#f8fafc',
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
    display: 'flex',
    alignItems: 'center',
    gap: '0.4rem',
  },
  youTag: {
    background: '#6366f1',
    color: '#fff',
    fontSize: '0.65rem',
    padding: '0.1rem 0.4rem',
    borderRadius: 999,
    fontWeight: 700,
    textTransform: 'uppercase',
    letterSpacing: '0.05em',
  },
  teamMeta: { fontSize: '0.75rem', color: '#94a3b8', marginTop: '0.15rem' },
  scoreCol: { textAlign: 'right', minWidth: 60 },
  score: {
    fontWeight: 800,
    fontSize: '1.2rem',
    color: '#f8fafc',
    lineHeight: 1,
  },
  scoreLabel: { fontSize: '0.7rem', color: '#94a3b8', marginTop: '0.1rem' },
  medal: { fontSize: '1.5rem' },
  position: { fontWeight: 700, color: '#94a3b8', fontSize: '0.95rem' },
  myTeamSummary: {
    position: 'fixed',
    left: 0, right: 0, bottom: 0,
    padding: '0.75rem 1rem',
    background: '#1e293b',
    borderTop: '1px solid #334155',
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
    fontSize: '0.85rem',
    color: '#cbd5e1',
  },
};
