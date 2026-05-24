import type { SessionInfo } from '../types';

interface Props {
  session: SessionInfo;
  nickname: string;
  onNicknameChange: (v: string) => void;
  onCreateTeam: () => void;
  onJoinTeam: () => void;
  onBack: () => void;
}

export function TeamChoiceScreen({ session, nickname, onNicknameChange, onCreateTeam, onJoinTeam, onBack }: Props) {
  const canProceed = nickname.trim().length >= 2;

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <button onClick={onBack} style={styles.back}>← Cambiar sesión</button>
        <div style={styles.sessionBadge}>
          <span style={styles.sessionName}>{session.name}</span>
          <span style={styles.sessionCode}>{session.accessCode}</span>
        </div>

        <h2 style={styles.heading}>¿Cómo te querés llamar?</h2>
        <input
          type="text"
          value={nickname}
          onChange={e => onNicknameChange(e.target.value)}
          placeholder="Tu apodo temporal"
          maxLength={20}
          autoComplete="off"
          style={styles.input}
        />

        <h2 style={{ ...styles.heading, marginTop: '1.25rem' }}>¿Qué querés hacer?</h2>
        <div style={styles.choices}>
          <button
            onClick={onCreateTeam}
            disabled={!canProceed}
            style={{ ...styles.choiceBtn, ...styles.createBtn, opacity: canProceed ? 1 : 0.4 }}
          >
            <span style={styles.choiceIcon}>➕</span>
            <span style={styles.choiceLabel}>Crear equipo</span>
            <span style={styles.choiceHint}>Generás un código para tus compañeros</span>
          </button>
          <button
            onClick={onJoinTeam}
            disabled={!canProceed}
            style={{ ...styles.choiceBtn, ...styles.joinBtn, opacity: canProceed ? 1 : 0.4 }}
          >
            <span style={styles.choiceIcon}>🔗</span>
            <span style={styles.choiceLabel}>Unirme a un equipo</span>
            <span style={styles.choiceHint}>Ingresás el código de tu compañero</span>
          </button>
        </div>
        {!canProceed && (
          <p style={styles.hint}>Ingresá tu apodo para continuar</p>
        )}
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
  back: {
    background: 'none', border: 'none', color: '#94a3b8',
    cursor: 'pointer', fontSize: '0.85rem', padding: 0, marginBottom: '1rem',
  },
  sessionBadge: {
    display: 'flex', justifyContent: 'space-between', alignItems: 'center',
    background: '#0f172a', borderRadius: 8, padding: '0.6rem 0.9rem', marginBottom: '1.25rem',
  },
  sessionName: { color: '#f8fafc', fontWeight: 600, fontSize: '0.95rem' },
  sessionCode: {
    color: '#6366f1', fontWeight: 700, fontSize: '1rem',
    letterSpacing: '0.15em', fontFamily: 'monospace',
  },
  heading: { color: '#f8fafc', fontSize: '1rem', fontWeight: 600, margin: '0 0 0.6rem' },
  input: {
    width: '100%', padding: '0 1rem', height: 48, fontSize: '1rem',
    background: '#0f172a', border: '2px solid #334155', borderRadius: 10,
    color: '#f8fafc', outline: 'none', boxSizing: 'border-box',
  },
  choices: { display: 'flex', flexDirection: 'column', gap: '0.75rem' },
  choiceBtn: {
    display: 'flex', flexDirection: 'column', alignItems: 'flex-start',
    padding: '1rem', borderRadius: 12, border: '2px solid transparent',
    cursor: 'pointer', textAlign: 'left', width: '100%',
  },
  createBtn: { background: '#312e81', borderColor: '#6366f1' },
  joinBtn: { background: '#1e3a5f', borderColor: '#3b82f6' },
  choiceIcon: { fontSize: '1.5rem', marginBottom: '0.25rem' },
  choiceLabel: { color: '#f8fafc', fontWeight: 700, fontSize: '1rem' },
  choiceHint: { color: '#94a3b8', fontSize: '0.8rem', marginTop: '0.2rem' },
  hint: { color: '#94a3b8', fontSize: '0.8rem', textAlign: 'center', marginTop: '0.75rem' },
};
