import { useState } from 'react';
import { sessionService } from '../../services/sessionService';
import type { SessionStatus } from '../../types/session';

interface Props {
  sessionId: string;
  status: SessionStatus;
  onStateChange: () => void;
}

type Action = 'start' | 'pause' | 'resume' | 'finalize';

const BUTTON_CONFIG: {
  action: Action;
  label: string;
  visibleIn: SessionStatus[];
  style: React.CSSProperties;
}[] = [
  {
    action: 'start',
    label: '▶ Iniciar',
    visibleIn: ['Pending'],
    style: { background: '#28a745', color: '#fff' },
  },
  {
    action: 'pause',
    label: '⏸ Pausar',
    visibleIn: ['InProgress'],
    style: { background: '#fd7e14', color: '#fff' },
  },
  {
    action: 'resume',
    label: '▶ Reanudar',
    visibleIn: ['Paused'],
    style: { background: '#007bff', color: '#fff' },
  },
  {
    action: 'finalize',
    label: '✔ Finalizar',
    visibleIn: ['InProgress', 'Paused'],
    style: { background: '#6c757d', color: '#fff' },
  },
];

const ACTION_FN: Record<Action, (id: string) => Promise<boolean>> = {
  start: sessionService.start.bind(sessionService),
  pause: sessionService.pause.bind(sessionService),
  resume: sessionService.resume.bind(sessionService),
  finalize: sessionService.finalize.bind(sessionService),
};

export function SessionControls({ sessionId, status, onStateChange }: Props) {
  const [loading, setLoading] = useState<Action | null>(null);
  const [error, setError] = useState<string | null>(null);

  const visible = BUTTON_CONFIG.filter(b => b.visibleIn.includes(status));
  if (visible.length === 0) return null;

  async function handleClick(action: Action) {
    setLoading(action);
    setError(null);
    try {
      await ACTION_FN[action](sessionId);
      onStateChange();
    } catch (e: unknown) {
      const msg = e && typeof e === 'object' && 'message' in e
        ? String((e as { message: unknown }).message)
        : 'Error al cambiar el estado';
      setError(msg);
    } finally {
      setLoading(null);
    }
  }

  return (
    <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
      {visible.map(btn => (
        <button
          key={btn.action}
          onClick={() => handleClick(btn.action)}
          disabled={loading !== null}
          style={{
            ...btn.style,
            padding: '0.35rem 0.9rem',
            border: 'none',
            borderRadius: 5,
            cursor: loading !== null ? 'not-allowed' : 'pointer',
            fontWeight: 'bold',
            fontSize: '0.85rem',
            opacity: loading !== null ? 0.7 : 1,
          }}
        >
          {loading === btn.action ? 'Procesando…' : btn.label}
        </button>
      ))}
      {error && (
        <span style={{ fontSize: '0.8rem', color: '#dc3545' }}>{error}</span>
      )}
    </div>
  );
}
