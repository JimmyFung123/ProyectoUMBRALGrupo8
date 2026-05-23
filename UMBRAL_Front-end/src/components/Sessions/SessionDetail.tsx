import { useEffect, useRef, useState } from 'react';
import { sessionService } from '../../services/sessionService';
import { SESSION_STATUS_LABELS, type SessionDetail, type Team } from '../../types/session';

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
    // Carga inicial
    fetchDetail(true);

    // Polling automático para reflejar cambios en tiempo real
    intervalRef.current = setInterval(() => fetchDetail(), POLL_INTERVAL_MS);

    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [sessionId]);

  if (loading) return <p style={{ padding: '2rem' }}>Cargando detalle de sesión…</p>;
  if (error)   return <p style={{ padding: '2rem', color: 'red' }}>{error}</p>;
  if (!detail) return null;

  const connectedCount = detail.teams.filter(t => t.isConnected).length;

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

      {/* ── Equipos ─────────────────────────────────────────────────── */}
      <section>
        <h2 style={{ marginTop: 0 }}>
          Equipos inscritos{' '}
          <span style={{ fontWeight: 'normal', fontSize: '0.9rem', color: '#555' }}>
            ({connectedCount}/{detail.teams.length} conectados)
          </span>
        </h2>

        {detail.teams.length === 0 ? (
          <p style={{ color: '#888' }}>Todavía no hay equipos registrados en esta sesión.</p>
        ) : (
          <ul style={{ listStyle: 'none', padding: 0 }}>
            {detail.teams.map(team => (
              <TeamRow key={team.id} team={team} />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

// ── TeamRow ───────────────────────────────────────────────────────────────────

function TeamRow({ team }: { team: Team }) {
  return (
    <li style={{
      display: 'flex',
      justifyContent: 'space-between',
      alignItems: 'center',
      padding: '0.75rem 1rem',
      marginBottom: '0.5rem',
      border: '1px solid #ddd',
      borderRadius: 8,
      background: team.isConnected ? '#f0fff4' : '#fafafa',
    }}>
      {/* Nombre + conexión */}
      <div style={{ display: 'flex', alignItems: 'center', gap: '0.6rem' }}>
        <span
          title={team.isConnected ? 'Conectado' : 'Desconectado'}
          style={{
            width: 10, height: 10, borderRadius: '50%',
            background: team.isConnected ? '#28a745' : '#aaa',
            display: 'inline-block',
            flexShrink: 0,
          }}
        />
        <strong>{team.name}</strong>
      </div>

      {/* Progreso */}
      <div style={{ display: 'flex', gap: '1.5rem', fontSize: '0.85rem', color: '#444' }}>
        <span>
          <span style={{ color: '#888' }}>Etapa: </span>
          <strong>
            {team.currentStageOrder === 0 ? 'Sin iniciar' : `#${team.currentStageOrder}`}
          </strong>
        </span>
        <span>
          <span style={{ color: '#888' }}>Pistas etapa: </span>
          <strong>{team.cluesReceivedCurrentStage}</strong>
        </span>
        <span>
          <span style={{ color: '#888' }}>Pistas total: </span>
          <strong>{team.totalCluesReceived}</strong>
        </span>
      </div>
    </li>
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
