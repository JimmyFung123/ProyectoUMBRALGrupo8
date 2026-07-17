import { useEffect, useRef, useState } from 'react';
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import type { ParticipantStage, SessionInfo, TeamCreatedInfo, TeamJoinedInfo } from '../types';
import { getTeamInfo } from '../services/teamService';
import { getParticipantStage } from '../services/sessionService';
import { SessionStatusOverlay, type BlockingSessionStatus } from './SessionStatusOverlay';

type TeamState = (TeamCreatedInfo & { isLeader: true }) | (TeamJoinedInfo & { isLeader: false });

interface Props {
  session: SessionInfo;
  team: TeamState;
  nickname: string;
  onGameStart: (stage: ParticipantStage) => void;
  /** Vuelve a la pantalla inicial y limpia el estado persistido. */
  onLeaveSession: () => void;
}

const POLL_INTERVAL_MS = 5_000;
const BLOCKING_STATUSES: ReadonlySet<string> = new Set(['Paused', 'Completed', 'Cancelled']);

const HUB_URL = (import.meta.env.VITE_SESSION_HUB_URL as string | undefined) ?? '/hubs/session';
const SERVER_TIMEOUT_MS = 6_000;
const KEEP_ALIVE_MS = 3_000;

export function WaitingRoomScreen({ session, team, nickname, onGameStart, onLeaveSession }: Props) {
  const inviteCode = team.inviteCode;
  const initialCount = team.isLeader ? 1 : (team as TeamJoinedInfo).memberCount;
  const [memberCount, setMemberCount] = useState(initialCount);
  const isReady = memberCount >= 2;
  const [copied, setCopied] = useState(false);
  // Cuando el operador pausa/finaliza/cancela mientras estamos en la sala de
  // espera, el polling lo detecta y mostramos el overlay bloqueante.
  const [blockingStatus, setBlockingStatus] = useState<BlockingSessionStatus | null>(null);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function poll() {
      try {
        // Update member count
        const info = await getTeamInfo(team.teamId);
        setMemberCount(info.memberCount);

        // Check session status: started → switch to game; paused/completed/cancelled → block.
        const stage = await getParticipantStage(session.id, team.teamId);
        if (BLOCKING_STATUSES.has(stage.sessionStatus)) {
          setBlockingStatus(stage.sessionStatus as BlockingSessionStatus);
          return;
        }
        setBlockingStatus(null);
        if (stage.sessionStatus === 'InProgress' && stage.type !== 'Waiting') {
          onGameStart(stage);
        }
      } catch {
        // silently ignore — stale data is better than crash
      }
    }

    poll();
    intervalRef.current = setInterval(poll, POLL_INTERVAL_MS);

    // Push refresh: SessionStateChanged fires on Start/Resume/Pause/Finalize/
    // Penalize/ForceAdvance/LeaveTeam — much faster than waiting for the next
    // 5s poll tick, especially for "the operator just started the session"
    // and "a teammate just left" (member count). Polling stays as fallback.
    const connection = new HubConnectionBuilder()
      .withUrl(HUB_URL)
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    connection.serverTimeoutInMilliseconds = SERVER_TIMEOUT_MS;
    connection.keepAliveIntervalInMilliseconds = KEEP_ALIVE_MS;
    connection.on('SessionStateChanged', () => { void poll(); });
    connection.onreconnected(() => {
      connection.invoke('JoinSession', session.id).catch(() => { /* swallow */ });
      void poll();
    });

    void (async () => {
      try {
        await connection.start();
        if (cancelled) { await connection.stop(); return; }
        await connection.invoke('JoinSession', session.id);
      } catch {
        // Hub unreachable — polling fallback keeps the UI fresh.
      }
    })();

    return () => {
      cancelled = true;
      if (intervalRef.current) clearInterval(intervalRef.current);
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.invoke('LeaveSession', session.id).catch(() => { /* swallow */ });
        connection.stop().catch(() => { /* swallow */ });
      }
    };
  }, [team.teamId, session.id, onGameStart]);

  function copyCode() {
    navigator.clipboard.writeText(inviteCode).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  }

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <button
          onClick={() => {
            // Confirmar siempre — si el líder se sale por accidente, el equipo
            // queda sin código de invitación visible. La confirmación también
            // existe en GameScreen, mantenemos el mismo contrato.
            const message = team.isLeader
              ? '¿Salir de la sesión? Sos el líder del equipo; los demás no podrán unirse con este código si te vas.'
              : '¿Salir de la sesión? Volverás a la pantalla de inicio.';
            if (window.confirm(message)) onLeaveSession();
          }}
          style={styles.exitBtn}
          aria-label="Salir de la sesión"
          title="Salir de la sesión"
        >
          ✕ Salir
        </button>
        <div style={styles.pulseIcon}>{isReady ? '✅' : '⏳'}</div>
        <h2 style={styles.title}>
          {isReady ? '¡Equipo listo!' : 'Sala de espera'}
        </h2>
        <p style={styles.subtitle}>
          Sos <strong style={{ color: '#6366f1' }}>{nickname}</strong>
          {' en '} <strong>{session.name}</strong>
        </p>

        {'isLeader' in team && team.isLeader && (
          <div style={styles.inviteBox}>
            <p style={styles.inviteLabel}>Compartí este código con tus compañeros:</p>
            <div style={styles.codeRow}>
              <span style={styles.codeText}>{inviteCode}</span>
              <button onClick={copyCode} style={styles.copyBtn}>
                {copied ? '✓ Copiado' : '📋 Copiar'}
              </button>
            </div>
          </div>
        )}

        <div style={styles.statusBox}>
          <div style={styles.memberCount}>{memberCount}</div>
          <div style={styles.memberLabel}>
            {memberCount === 1 ? 'integrante' : 'integrantes'} en el equipo
          </div>
          {!isReady && (
            <p style={styles.waitMsg}>Esperando que se unan más compañeros…</p>
          )}
          {isReady && (
            <p style={styles.readyMsg}>El profesor iniciará la actividad en breve.</p>
          )}
        </div>
      </div>

      {blockingStatus && (
        <SessionStatusOverlay
          status={blockingStatus}
          onLeaveSession={onLeaveSession}
        />
      )}
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    minHeight: '100dvh', display: 'flex', alignItems: 'center',
    justifyContent: 'center', padding: '1rem', background: '#0f172a',
  },
  card: {
    width: '100%', maxWidth: 420, padding: '2rem 1.5rem',
    background: '#1e293b', borderRadius: 16, textAlign: 'center',
    boxShadow: '0 8px 32px rgba(0,0,0,0.4)',
    position: 'relative',
  },
  exitBtn: {
    position: 'absolute',
    top: '0.75rem',
    right: '0.75rem',
    padding: '0.35rem 0.75rem',
    background: 'transparent',
    color: '#94a3b8',
    border: '1px solid #334155',
    borderRadius: 999,
    fontSize: '0.75rem',
    fontWeight: 600,
    cursor: 'pointer',
  },
  pulseIcon: { fontSize: '3rem', marginBottom: '0.5rem' },
  title: { color: '#f8fafc', fontSize: '1.5rem', fontWeight: 800, margin: '0 0 0.5rem' },
  subtitle: { color: '#94a3b8', fontSize: '0.9rem', margin: '0 0 1.5rem' },
  inviteBox: {
    background: '#0f172a', borderRadius: 12, padding: '1rem', marginBottom: '1.5rem',
  },
  inviteLabel: { color: '#94a3b8', fontSize: '0.8rem', margin: '0 0 0.75rem' },
  codeRow: { display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.75rem' },
  codeText: {
    color: '#6366f1', fontSize: '2rem', fontWeight: 900,
    letterSpacing: '0.3em', fontFamily: 'monospace',
  },
  copyBtn: {
    padding: '0.4rem 0.75rem', background: '#334155', color: '#f8fafc',
    border: 'none', borderRadius: 8, cursor: 'pointer', fontSize: '0.8rem',
  },
  statusBox: {
    background: '#0f172a', borderRadius: 12, padding: '1.5rem',
  },
  memberCount: { color: '#f8fafc', fontSize: '3rem', fontWeight: 900 },
  memberLabel: { color: '#94a3b8', fontSize: '0.9rem', marginBottom: '0.75rem' },
  waitMsg: { color: '#fbbf24', fontSize: '0.85rem', margin: 0 },
  readyMsg: { color: '#34d399', fontSize: '0.85rem', margin: 0 },
};
