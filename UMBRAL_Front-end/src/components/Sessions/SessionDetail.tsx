import { useEffect, useRef, useState } from 'react';
import { sessionService } from '../../services/sessionService';
import { SESSION_STATUS_LABELS, type SessionDetail } from '../../types/session';
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  Spinner,
  type BadgeTone,
} from '../ui';

interface Props {
  sessionId: string;
  onBack: () => void;
}

const POLL_INTERVAL_MS = 10_000;

const STATUS_TONES: Record<string, BadgeTone> = {
  Pending: 'warning', InProgress: 'brand', Paused: 'info', Completed: 'success', Cancelled: 'danger',
};

function StatusBadge({ status }: { status: string }) {
  return (
    <Badge tone={STATUS_TONES[status] ?? 'neutral'} variant="solid">
      {SESSION_STATUS_LABELS[status as keyof typeof SESSION_STATUS_LABELS] ?? status}
    </Badge>
  );
}

export function SessionDetailView({ sessionId, onBack }: Props) {
  const [detail, setDetail] = useState<SessionDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  async function fetchDetail(showLoading = false) {
    if (showLoading) setLoading(true);
    setError(null);
    try {
      const data = await sessionService.getDetail(sessionId);
      setDetail(data);
    } catch {
      setError('No se pudo cargar el detalle de la sesión.');
    } finally {
      if (showLoading) setLoading(false);
    }
  }

  useEffect(() => {
    fetchDetail(true);
    intervalRef.current = setInterval(() => fetchDetail(), POLL_INTERVAL_MS);
    return () => { if (intervalRef.current) clearInterval(intervalRef.current); };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [sessionId]);

  async function copy() {
    if (!detail?.accessCode) return;
    try {
      await navigator.clipboard.writeText(detail.accessCode);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch { /* ignored */ }
  }

  if (loading) return <Card><Spinner label="Cargando detalle de sesión…" /></Card>;
  if (error)   return <Alert tone="danger">{error}</Alert>;
  if (!detail) return null;

  return (
    <div>
      <div className="flex items-center gap-3 mb-5 flex-wrap">
        <Button variant="ghost" size="sm" onClick={onBack} leadingIcon="←">Volver</Button>
        <h1 className="text-xl md:text-2xl font-bold text-ink leading-tight">{detail.name}</h1>
        <StatusBadge status={detail.status} />
      </div>

      {detail.accessCode && (
        <Card accent="brand" className="mb-5">
          <div className="flex items-center gap-4 flex-wrap">
            <div>
              <p className="text-xs font-semibold uppercase tracking-wider text-brand-700">
                Código de acceso para participantes
              </p>
              <p className="text-3xl font-extrabold tracking-[0.3em] text-brand-700 font-mono mt-1">
                {detail.accessCode}
              </p>
            </div>
            <div className="ml-auto">
              <Button variant="secondary" onClick={copy}>
                {copied ? '✓ Copiado' : 'Copiar'}
              </Button>
            </div>
          </div>
        </Card>
      )}

      <Card>
        <CardHeader
          title="Metadata"
          description={`Actualización automática cada ${POLL_INTERVAL_MS / 1000} s`}
        />
        <dl className="grid grid-cols-1 sm:grid-cols-2 gap-3 text-sm">
          <Field
            label="Estado"
            value={SESSION_STATUS_LABELS[detail.status as keyof typeof SESSION_STATUS_LABELS] ?? detail.status}
          />
          <Field
            label="Creada"
            value={new Date(detail.createdAt).toLocaleString('es-VE', { dateStyle: 'medium', timeStyle: 'short' })}
          />
          {detail.scheduledAt && (
            <Field
              label="Programada"
              value={new Date(detail.scheduledAt).toLocaleString('es-VE', { dateStyle: 'medium', timeStyle: 'short' })}
            />
          )}
        </dl>
      </Card>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wider text-ink-muted font-semibold">{label}</dt>
      <dd className="text-ink mt-0.5">{value}</dd>
    </div>
  );
}
