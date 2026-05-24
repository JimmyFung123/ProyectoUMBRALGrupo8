import { useState } from 'react';
import { joinTeam } from '../services/teamService';
import type { SessionInfo, TeamJoinedInfo } from '../types';

interface Props {
  session: SessionInfo;
  nickname: string;
  onTeamJoined: (team: TeamJoinedInfo) => void;
  onBack: () => void;
}

export function JoinTeamScreen({ session, nickname, onTeamJoined, onBack }: Props) {
  const [inviteCode, setInviteCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (inviteCode.trim().length < 3) return;
    setLoading(true);
    setError('');
    try {
      const result = await joinTeam(inviteCode.trim());
      onTeamJoined({
        teamId: result.teamId,
        teamName: result.teamName,
        inviteCode: result.inviteCode,
        memberCount: result.memberCount,
      });
    } catch {
      setError('Código inválido. Pedile el código al líder de tu equipo.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <button onClick={onBack} style={styles.back}>← Volver</button>
        <div style={styles.icon}>🔗</div>
        <h2 style={styles.title}>Unirme a un equipo</h2>
        <p style={styles.subtitle}>
          Sos <strong style={{ color: '#3b82f6' }}>{nickname}</strong> en la sesión <strong>{session.name}</strong>
        </p>

        <form onSubmit={handleSubmit} style={styles.form}>
          <label style={styles.label}>Código del equipo</label>
          <input
            type="text"
            value={inviteCode}
            onChange={e => setInviteCode(e.target.value.toUpperCase())}
            placeholder="Ej: XY42"
            maxLength={6}
            autoCapitalize="characters"
            autoComplete="off"
            style={styles.input}
          />
          {error && <p style={styles.error}>{error}</p>}
          <button
            type="submit"
            disabled={loading || inviteCode.trim().length < 3}
            style={{ ...styles.button, opacity: loading || inviteCode.trim().length < 3 ? 0.5 : 1 }}
          >
            {loading ? 'Uniéndose…' : 'Unirme al equipo'}
          </button>
        </form>
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
    width: '100%', maxWidth: 420, padding: '1.5rem',
    background: '#1e293b', borderRadius: 16, boxShadow: '0 8px 32px rgba(0,0,0,0.4)',
  },
  back: { background: 'none', border: 'none', color: '#94a3b8', cursor: 'pointer', fontSize: '0.85rem', padding: 0, marginBottom: '1rem' },
  icon: { fontSize: '2.5rem', textAlign: 'center' },
  title: { color: '#f8fafc', textAlign: 'center', fontSize: '1.4rem', fontWeight: 700, margin: '0.5rem 0' },
  subtitle: { color: '#94a3b8', textAlign: 'center', fontSize: '0.9rem', margin: '0 0 1.25rem' },
  form: { display: 'flex', flexDirection: 'column', gap: '0.75rem' },
  label: { color: '#94a3b8', fontSize: '0.85rem', fontWeight: 600 },
  input: {
    width: '100%', padding: '0 1rem', height: 52, fontSize: '1.8rem',
    letterSpacing: '0.25em', textAlign: 'center', textTransform: 'uppercase',
    background: '#0f172a', border: '2px solid #334155', borderRadius: 10,
    color: '#f8fafc', outline: 'none', boxSizing: 'border-box',
  },
  error: { color: '#f87171', fontSize: '0.85rem', margin: 0 },
  button: {
    width: '100%', height: 52, fontSize: '1rem', fontWeight: 700,
    background: '#3b82f6', color: '#fff', border: 'none', borderRadius: 10, cursor: 'pointer',
  },
};
