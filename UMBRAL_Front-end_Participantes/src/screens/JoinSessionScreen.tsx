import { useState } from 'react';
import { getSessionByCode } from '../services/sessionService';
import type { SessionInfo } from '../types';

interface Props {
  onSessionFound: (session: SessionInfo) => void;
}

export function JoinSessionScreen({ onSessionFound }: Props) {
  const [code, setCode] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (code.trim().length < 4) return;
    setLoading(true);
    setError('');
    try {
      const session = await getSessionByCode(code);
      onSessionFound(session);
    } catch {
      setError('Código inválido. Verificá el código con tu profesor.');
    } finally {
      setLoading(false);
    }
  }

  return (
    <div style={styles.container}>
      <div style={styles.card}>
        <div style={styles.logo}>🎯</div>
        <h1 style={styles.title}>UMBRAL</h1>
        <p style={styles.subtitle}>Ingresá el código de sesión que te dio tu profesor</p>

        <form onSubmit={handleSubmit} style={styles.form}>
          <input
            type="text"
            value={code}
            onChange={e => setCode(e.target.value.toUpperCase())}
            placeholder="Ej: ABC123"
            maxLength={8}
            autoCapitalize="characters"
            autoComplete="off"
            style={styles.input}
          />
          {error && <p style={styles.error}>{error}</p>}
          <button
            type="submit"
            disabled={loading || code.trim().length < 4}
            style={{ ...styles.button, opacity: loading || code.trim().length < 4 ? 0.5 : 1 }}
          >
            {loading ? 'Buscando…' : 'Entrar a la sesión'}
          </button>
        </form>
      </div>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  container: {
    minHeight: '100dvh',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '1rem',
    background: '#0f172a',
  },
  card: {
    width: '100%',
    maxWidth: 420,
    padding: '2rem 1.5rem',
    background: '#1e293b',
    borderRadius: 16,
    textAlign: 'center',
    boxShadow: '0 8px 32px rgba(0,0,0,0.4)',
  },
  logo: { fontSize: '3rem', marginBottom: '0.5rem' },
  title: { color: '#f8fafc', fontSize: '2rem', fontWeight: 800, margin: '0 0 0.5rem' },
  subtitle: { color: '#94a3b8', fontSize: '0.95rem', margin: '0 0 1.5rem', lineHeight: 1.5 },
  form: { display: 'flex', flexDirection: 'column', gap: '0.75rem' },
  input: {
    width: '100%', padding: '0 1rem', height: 52, fontSize: '1.5rem',
    letterSpacing: '0.2em', textAlign: 'center', textTransform: 'uppercase',
    background: '#0f172a', border: '2px solid #334155', borderRadius: 10,
    color: '#f8fafc', outline: 'none', boxSizing: 'border-box',
  },
  error: { color: '#f87171', fontSize: '0.85rem', margin: 0 },
  button: {
    width: '100%', padding: '0 1rem', height: 52, fontSize: '1rem',
    fontWeight: 700, background: '#6366f1', color: '#fff',
    border: 'none', borderRadius: 10, cursor: 'pointer',
  },
};
