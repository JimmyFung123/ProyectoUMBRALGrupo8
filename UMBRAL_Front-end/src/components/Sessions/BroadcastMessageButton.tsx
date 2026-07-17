import { useState } from 'react';
import { sessionService } from '../../services/sessionService';
import type { SessionStatus } from '../../types/session';
import {
  Alert,
  Button,
  FormField,
  Modal,
  Textarea,
} from '../ui';

interface Props {
  sessionId: string;
  status: SessionStatus;
}

const MAX_LENGTH = 240;

/**
 * HU-28 — opens a modal where the operator can type a short message that
 * gets pushed to every participant via SignalR. Only enabled while the
 * session is InProgress or Paused; outside that window the backend rejects
 * the call so we disable the button preemptively.
 */
export function BroadcastMessageButton({ sessionId, status }: Props) {
  const [open, setOpen] = useState(false);
  const [message, setMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastSent, setLastSent] = useState<string | null>(null);

  const canBroadcast = status === 'InProgress' || status === 'Paused';

  function openDialog() {
    setMessage('');
    setError(null);
    setOpen(true);
  }

  async function handleSubmit() {
    const trimmed = message.trim();
    if (!trimmed) {
      setError('El mensaje no puede estar vacío.');
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      const result = await sessionService.broadcastMessage(sessionId, trimmed);
      setLastSent(result.message);
      setOpen(false);
    } catch (err: unknown) {
      const apiErr = err as { code?: string; message?: string };
      setError(apiErr?.message ?? 'No se pudo enviar el mensaje.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <Button
        variant="secondary"
        size="sm"
        onClick={openDialog}
        disabled={!canBroadcast}
        leadingIcon="📩"
        title={
          canBroadcast
            ? 'Enviar un mensaje en vivo a todos los participantes'
            : 'Solo se pueden enviar mensajes con la sesión en curso o pausada'
        }
      >
        Enviar mensaje
      </Button>

      <Modal
        open={open}
        onClose={() => !submitting && setOpen(false)}
        title="📩 Enviar mensaje a participantes"
        description="El mensaje aparece como una notificación animada en la pantalla de cada participante conectado y queda registrado en la auditoría."
        footer={
          <>
            <Button
              variant="secondary"
              onClick={() => setOpen(false)}
              disabled={submitting}
            >
              Cancelar
            </Button>
            <Button
              onClick={handleSubmit}
              disabled={submitting || !message.trim()}
              leadingIcon="📤"
            >
              {submitting ? 'Enviando…' : 'Enviar'}
            </Button>
          </>
        }
      >
        <FormField
          label="Mensaje"
          htmlFor="broadcast-msg"
          required
          hint={`${message.length}/${MAX_LENGTH} caracteres`}
        >
          <Textarea
            id="broadcast-msg"
            rows={3}
            maxLength={MAX_LENGTH}
            placeholder="Ej: ¡Quedan 5 minutos! Apúrense con la última etapa."
            value={message}
            onChange={e => setMessage(e.target.value)}
            autoFocus
          />
        </FormField>
        {error && <Alert tone="danger" className="mt-3">{error}</Alert>}
      </Modal>

      {lastSent && (
        <div className="mt-2">
          <Alert
            tone="success"
            onDismiss={() => setLastSent(null)}
          >
            Mensaje entregado: «{lastSent}»
          </Alert>
        </div>
      )}
    </>
  );
}
