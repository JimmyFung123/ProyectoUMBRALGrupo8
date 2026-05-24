import { useEffect, useRef, useState } from 'react';
import type { ParticipantStage, SessionInfo, TeamCreatedInfo, TeamJoinedInfo } from '../types';
import { getTeamInfo } from '../services/teamService';
import { getParticipantStage } from '../services/sessionService';

type TeamState = (TeamCreatedInfo & { isLeader: true }) | (TeamJoinedInfo & { isLeader: false });

interface Props {
  session: SessionInfo;
  team: TeamState;
  nickname: string;
  onGameStart: (stage: ParticipantStage) => void;
}

const POLL_INTERVAL_MS = 5_000;

export function WaitingRoomScreen({ session, team, nickname, onGameStart }: Props) {
  const inviteCode = team.inviteCode;
  const initialCount = team.isLeader ? 1 : (team as TeamJoinedInfo).memberCount;
  const [memberCount, setMemberCount] = useState(initialCount);
  const isReady = memberCount >= 2;
  const [copied, setCopied] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    async function poll() {
      try {
        // Update member count
        const info = await getTeamInfo(team.teamId);
        setMemberCount(info.memberCount);

        // Check if the session has started and the team has been assigned a stage
        const stage = await getParticipantStage(session.id, team.teamId);
        if (stage.sessionStatus === 'InProgress' && stage.type !== 'Waiting') {
          onGameStart(stage);
        }
      } catch {
        // silently ignore — stale data is better than crash
      }
    }

    poll();
    intervalRef.current = setInterval(poll, POLL_INTERVAL_MS);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
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
