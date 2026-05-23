import { useEffect, useState } from 'react';
import { missionService } from '../../services/missionService';
import { sessionService } from '../../services/sessionService';
import type { Mission } from '../../types/mission';
import type { ApiError } from '../../types/mission';
import { SESSION_STATUS_LABELS, type CreateSessionPayload, type Session } from '../../types/session';

const initialForm: CreateSessionPayload = {
  missionId: '',
  name: '',
  scheduledAt: null,
};

// ── SessionList ───────────────────────────────────────────────────────────────

interface Props {
  onViewDetail: (sessionId: string) => void;
}

export function SessionList({ onViewDetail }: Props) {
  const [sessions, setSessions] = useState<Session[]>([]);
  const [missions, setMissions] = useState<Mission[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [form, setForm] = useState<CreateSessionPayload>(initialForm);
  const [createError, setCreateError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  useEffect(() => {
    loadAll();
  }, []);

  async function loadAll() {
    setLoading(true);
    setError(null);
    try {
      const [s, m] = await Promise.all([
        sessionService.getAll(),
        missionService.getAll(),
      ]);
      setSessions(s);
      // Solo las misiones activas pueden tener sesiones
      setMissions(m.filter(m => m.status === 'Active'));
    } catch {
      setError('No se pudieron cargar las sesiones. Intentá de nuevo.');
    } finally {
      setLoading(false);
    }
  }

  async function handleCreate(e: React.FormEvent) {
    e.preventDefault();
    setCreateError(null);
    setCreating(true);
    try {
      await sessionService.create(form);
      setForm(initialForm);
      await loadAll();
    } catch (err) {
      setCreateError((err as ApiError)?.message ?? 'No se pudo crear la sesión.');
    } finally {
      setCreating(false);
    }
  }

  // ── Renderizado ───────────────────────────────────────────────────────────

  return (
    <div style={{ maxWidth: 860, margin: '0 auto', padding: '2rem', fontFamily: 'sans-serif' }}>
      <h1>Sesiones</h1>

      {/* ── Formulario de creación ─────────────────────────────── */}
      <section style={{ marginBottom: '2rem', padding: '1rem', border: '1px solid #ddd', borderRadius: 8 }}>
        <h2 style={{ marginTop: 0 }}>Nueva sesión</h2>
        <form onSubmit={handleCreate}>
          <div style={{ marginBottom: '0.75rem' }}>
            <label>Misión (activa)</label>
            <select
              required
              value={form.missionId}
              onChange={e => setForm(f => ({ ...f, missionId: e.target.value }))}
              style={{ display: 'block', width: '100%', padding: '0.4rem' }}
            >
              <option value="">— Seleccioná una misión —</option>
              {missions.map(m => (
                <option key={m.id} value={m.id}>{m.name}</option>
              ))}
            </select>
            {missions.length === 0 && (
              <small style={{ color: '#888' }}>No hay misiones activas. Activá una primero.</small>
            )}
          </div>

          <div style={{ marginBottom: '0.75rem' }}>
            <label>Nombre de la sesión</label>
            <input
              required
              value={form.name}
              onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
              placeholder="Ej: Ronda 1 — Equipo A"
              style={{ display: 'block', width: '100%', padding: '0.4rem' }}
            />
          </div>

          <div style={{ marginBottom: '0.75rem' }}>
            <label>Fecha programada (opcional)</label>
            <input
              type="datetime-local"
              value={form.scheduledAt ?? ''}
              onChange={e => setForm(f => ({ ...f, scheduledAt: e.target.value || null }))}
              style={{ display: 'block', padding: '0.4rem' }}
            />
          </div>

          {createError && <p style={{ color: 'red', margin: '0.4rem 0' }}>{createError}</p>}
          <button type="submit" disabled={creating}>
            {creating ? 'Creando…' : 'Crear sesión'}
          </button>
        </form>
      </section>

      {/* ── Lista ─────────────────────────────────────────────── */}
      {loading && <p>Cargando sesiones…</p>}
      {error && <p style={{ color: 'red' }}>{error}</p>}
      {!loading && !error && sessions.length === 0 && (
        <p>Todavía no hay sesiones. Creá la primera arriba.</p>
      )}

      <ul style={{ listStyle: 'none', padding: 0 }}>
        {sessions.map(session => {
          const missionName = missions.find(m => m.id === session.missionId)?.name ?? session.missionId;
          return (
            <li
              key={session.id}
              style={{ marginBottom: '0.75rem', border: '1px solid #ddd', borderRadius: 8, padding: '0.9rem 1rem' }}
            >
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                  <strong>{session.name}</strong>
                  <StatusBadge status={session.status} />
                  <p style={{ margin: '0.2rem 0', color: '#555', fontSize: '0.9rem' }}>
                    Misión: <em>{missionName}</em>
                  </p>
                  <small style={{ color: '#888' }}>
                    Creada: {new Date(session.createdAt).toLocaleDateString('es-VE', { dateStyle: 'medium' })}
                    {session.scheduledAt && (
                      <> · Programada: {new Date(session.scheduledAt).toLocaleString('es-VE', { dateStyle: 'medium', timeStyle: 'short' })}</>
                    )}
                  </small>
                </div>
                <button
                  onClick={() => onViewDetail(session.id)}
                  style={{ cursor: 'pointer', padding: '0.3rem 0.8rem', whiteSpace: 'nowrap' }}
                >
                  Ver detalle
                </button>
              </div>
            </li>
          );
        })}
      </ul>
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
      marginLeft: '0.5rem',
      padding: '0.1rem 0.5rem',
      borderRadius: 4,
      fontSize: '0.75rem',
      background: colors.bg,
      color: colors.text,
    }}>
      {SESSION_STATUS_LABELS[status as keyof typeof SESSION_STATUS_LABELS] ?? status}
    </span>
  );
}
