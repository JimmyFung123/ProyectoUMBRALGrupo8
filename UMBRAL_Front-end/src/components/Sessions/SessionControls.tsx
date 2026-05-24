import { useState } from 'react';
import { sessionService } from '../../services/sessionService';
import type { SessionStatus } from '../../types/session';

interface Props {
  sessionId: string;
  status: SessionStatus;
  teamsCount: number;
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

export function SessionControls({ sessionId, status, teamsCount, onStateChange }: Props) {
  const [loading, setLoading] = useState<Action | null>(null);
  const [error, setError] = useState<string | null>(null);

  const visible = BUTTON_CONFIG.filter(b => b.visibleIn.includes(status));
  if (visible.length === 0) return null;

  // HU-12: cannot start without at least 1 team enrolled
  const startBlocked = status === 'Pending' && teamsCount === 0;

  async function handleClick(action: Action) {
    setLoading(action);
    setError(null);
    try {
      await ACTION_FN[action](sessionId);
      onStateChange();
    } catch (e: unknown) {
      const raw = e && typeof e === 'object' && 'message' in e
        ? String((e as { message: unknown }).message)
        : 'Error al cambiar el estado';
      setError(raw);
    } finally {
      setLoading(null);
    }
  }

  return (
    <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center', flexWrap: 'wrap' }}>
      {visible.map(btn => {
        const isStartBtn = btn.action === 'start';
        const isDisabled = loading !== null || (isStartBtn && startBlocked);

        return (
          <button
            key={btn.action}
            onClick={() => !isDisabled && handleClick(btn.action)}
            disabled={isDisabled}
            title={isStartBtn && startBlocked ? 'Debe haber al menos un equipo inscrito para iniciar la sesión' : undefined}
            style={{
              ...btn.style,
              padding: '0.35rem 0.9rem',
              border: 'none',
              borderRadius: 5,
              cursor: isDisabled ? 'not-allowed' : 'pointer',
              fontWeight: 'bold',
              fontSize: '0.85rem',
              opacity: isDisabled ? 0.5 : 1,
            }}
          >
            {loading === btn.action ? 'Procesando…' : btn.label}
          </button>
        );
      })}

      {startBlocked && (
        <span style={{ fontSize: '0.8rem', color: '#856404', background: '#fff3cd', padding: '0.2rem 0.6rem', borderRadius: 4 }}>
          ⚠ Sin equipos inscritos — no se puede iniciar
        </span>
      )}

      {error && (
        <span style={{ fontSize: '0.8rem', color: '#dc3545' }}>{error}</span>
      )}
    </div>
  );
}
