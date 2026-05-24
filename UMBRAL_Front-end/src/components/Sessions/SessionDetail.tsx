import { useEffect, useRef, useState } from 'react';
import { sessionService } from '../../services/sessionService';
import { SESSION_STATUS_LABELS, type SessionDetail } from '../../types/session';

interface Props {
  sessionId: string;
  onBack: () => void;
}

const POLL_INTERVAL_MS = 10_000;

// ── SessionDetail ─────────────────────────────────────────────────────────────

export function SessionDetailView({ sessionId, onBack }: Props) {
  const [detail, setDetail] = useState<SessionDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
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
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [sessionId]);

  if (loading) return <p style={{ padding: '2rem' }}>Cargando detalle de sesión…</p>;
  if (error)   return <p style={{ padding: '2rem', color: 'red' }}>{error}</p>;
  if (!detail) return null;

  return (
    <div style={{ maxWidth: 860, margin: '0 auto', padding: '2rem', fontFamily: 'sans-serif' }}>

      {/* ── Encabezado ──────────────────────────────────────────────── */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', marginBottom: '1.5rem' }}>
        <button onClick={onBack} style={{ cursor: 'pointer', padding: '0.3rem 0.7rem' }}>
          ← Volver
        </button>
        <h1 style={{ margin: 0 }}>{detail.name}</h1>
        <StatusBadge status={detail.status} />
      </div>

      {/* ── Código de acceso ────────────────────────────────────────── */}
      {detail.accessCode && (
        <section style={{
          marginBottom: '1.5rem', padding: '1rem 1.5rem',
          background: '#eef2ff', border: '2px solid #6366f1', borderRadius: 8,
          display: 'flex', alignItems: 'center', gap: '1.5rem',
        }}>
          <div>
            <p style={{ margin: 0, fontSize: '0.75rem', color: '#6366f1', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
              Código de acceso para participantes
            </p>
            <p style={{ margin: '0.25rem 0 0', fontSize: '2rem', fontWeight: 700, letterSpacing: '0.3em', color: '#3730a3', fontFamily: 'monospace' }}>
              {detail.accessCode}
            </p>
          </div>
          <button
            onClick={() => navigator.clipboard.writeText(detail.accessCode!)}
            style={{ marginLeft: 'auto', cursor: 'pointer', padding: '0.4rem 0.9rem', borderRadius: 6, border: '1px solid #6366f1', background: 'white', color: '#6366f1', fontWeight: 600 }}
          >
            Copiar
          </button>
        </section>
      )}

      {/* ── Metadata ────────────────────────────────────────────────── */}
      <section style={{ marginBottom: '1.5rem', padding: '1rem', background: '#f8f9fa', borderRadius: 8 }}>
        <p style={{ margin: '0.2rem 0' }}>
          <strong>Estado:</strong>{' '}
          {SESSION_STATUS_LABELS[detail.status as keyof typeof SESSION_STATUS_LABELS] ?? detail.status}
        </p>
        <p style={{ margin: '0.2rem 0' }}>
          <strong>Creada:</strong>{' '}
          {new Date(detail.createdAt).toLocaleString('es-VE', { dateStyle: 'medium', timeStyle: 'short' })}
        </p>
        {detail.scheduledAt && (
          <p style={{ margin: '0.2rem 0' }}>
            <strong>Programada:</strong>{' '}
            {new Date(detail.scheduledAt).toLocaleString('es-VE', { dateStyle: 'medium', timeStyle: 'short' })}
          </p>
        )}
        <p style={{ margin: '0.2rem 0', color: '#555', fontSize: '0.85rem' }}>
          Actualización automática cada {POLL_INTERVAL_MS / 1000} s
        </p>
      </section>
    </div>
  );
}

// ── StatusBadge ───────────────────────────────────────────────────────────────

const STATUS_COLORS: Record<string, { bg: string; text: string }> = {
  Pending:    { bg: '#fff3cd', text: '#856404' },
  InProgress: { bg: '#cce5ff', text: '#004085' },
  Completed:  { bg: '#d4edda', text: '#155724' },
  Cancelled:  { bg: '#f8d7da', text: '#721c24' },
};

function StatusBadge({ status }: { status: string }) {
  const colors = STATUS_COLORS[status] ?? { bg: '#eee', text: '#333' };
  return (
    <span style={{
      padding: '0.15rem 0.6rem',
      borderRadius: 4,
      fontSize: '0.8rem',
      background: colors.bg,
      color: colors.text,
      fontWeight: 600,
    }}>
      {SESSION_STATUS_LABELS[status as keyof typeof SESSION_STATUS_LABELS] ?? status}
    </span>
  );
}
