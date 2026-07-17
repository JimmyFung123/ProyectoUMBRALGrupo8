import { useState } from 'react';
import { sessionService } from '../../services/sessionService';
import type { SessionStatus } from '../../types/session';
import { Alert, Badge, Button, type ButtonVariant } from '../ui';
import { BroadcastMessageButton } from './BroadcastMessageButton';

interface Props {
  sessionId: string;
  status: SessionStatus;
  teamsCount: number;
  onStateChange: () => void;
}

type Action = 'start' | 'pause' | 'resume' | 'finalize';

interface ButtonDef {
  action: Action;
  label: string;
  icon: string;
  visibleIn: SessionStatus[];
  variant: ButtonVariant;
}

const BUTTONS: ButtonDef[] = [
  { action: 'start',    label: 'Iniciar',   icon: '▶', visibleIn: ['Pending'],               variant: 'primary'   },
  { action: 'pause',    label: 'Pausar',    icon: '⏸', visibleIn: ['InProgress'],            variant: 'secondary' },
  { action: 'resume',   label: 'Reanudar',  icon: '▶', visibleIn: ['Paused'],                variant: 'primary'   },
  { action: 'finalize', label: 'Finalizar', icon: '✔', visibleIn: ['InProgress', 'Paused'],  variant: 'danger'    },
];

const ACTION_FN: Record<Action, (id: string) => Promise<boolean>> = {
  start:    sessionService.start.bind(sessionService),
  pause:    sessionService.pause.bind(sessionService),
  resume:   sessionService.resume.bind(sessionService),
  finalize: sessionService.finalize.bind(sessionService),
};

export function SessionControls({ sessionId, status, teamsCount, onStateChange }: Props) {
  const [loading, setLoading] = useState<Action | null>(null);
  const [error, setError] = useState<string | null>(null);

  const visible = BUTTONS.filter(b => b.visibleIn.includes(status));
  const canBroadcast = status === 'InProgress' || status === 'Paused';
  if (visible.length === 0 && !canBroadcast) return null;

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
    <div className="flex flex-col gap-2">
      <div className="flex items-center gap-2 flex-wrap">
        {visible.map(btn => {
          const isStartBtn = btn.action === 'start';
          const isDisabled = loading !== null || (isStartBtn && startBlocked);
          return (
            <Button
              key={btn.action}
              variant={btn.variant}
              size="sm"
              disabled={isDisabled}
              onClick={() => !isDisabled && handleClick(btn.action)}
              title={isStartBtn && startBlocked ? 'Debe haber al menos un equipo inscrito para iniciar la sesión' : undefined}
              leadingIcon={btn.icon}
            >
              {loading === btn.action ? 'Procesando…' : btn.label}
            </Button>
          );
        })}
        {startBlocked && (
          <Badge tone="warning">
            ⚠ Sin equipos inscritos — no se puede iniciar
          </Badge>
        )}
        {canBroadcast && (
          <BroadcastMessageButton sessionId={sessionId} status={status} />
        )}
      </div>
      {error && <Alert tone="danger">{error}</Alert>}
    </div>
  );
}
