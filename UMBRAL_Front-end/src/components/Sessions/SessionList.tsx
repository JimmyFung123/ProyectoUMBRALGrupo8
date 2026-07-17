import { useEffect, useState } from 'react';
import { missionService } from '../../services/missionService';
import { sessionService } from '../../services/sessionService';
import type { Mission } from '../../types/mission';
import type { ApiError } from '../../types/mission';
import { SESSION_STATUS_LABELS, type CreateSessionPayload, type Session, type SessionStatus } from '../../types/session';
import {
  Alert,
  Badge,
  Button,
  Card,
  CardHeader,
  EmptyState,
  FormField,
  PageHeader,
  Select,
  Spinner,
  Stack,
  TextInput,
  type BadgeTone,
} from '../ui';

const initialForm: CreateSessionPayload = {
  missionId: '',
  name: '',
  scheduledAt: null,
};

interface EditForm {
  name: string;
  scheduledAt: string | null;
}

interface Props {
  onViewDetail: (sessionId: string) => void;
  /** false = modo consulta (Administrador, RB-10): oculta crear/editar/cancelar. */
  canManage: boolean;
}

export function SessionList({ onViewDetail, canManage }: Props) {
  const [sessions, setSessions] = useState<Session[]>([]);
  const [missions, setMissions] = useState<Mission[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [form, setForm] = useState<CreateSessionPayload>(initialForm);
  const [createError, setCreateError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editForm, setEditForm] = useState<EditForm>({ name: '', scheduledAt: null });
  const [editError, setEditError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const [cancellingId, setCancellingId] = useState<string | null>(null);

  useEffect(() => { loadAll(); }, []);

  async function loadAll() {
    setLoading(true);
    setError(null);
    try {
      const [s, m] = await Promise.all([
        sessionService.getAll(),
        missionService.getAll(),
      ]);
      setSessions(s);
      setMissions(m);
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

  async function handleCancel(session: Session) {
    const confirmed = window.confirm(
      `¿Cancelar la sesión "${session.name}"?\n\nSe eliminarán todos los equipos registrados. Esta acción no se puede deshacer.`,
    );
    if (!confirmed) return;

    setCancellingId(session.id);
    try {
      await sessionService.cancel(session.id);
      await loadAll();
    } catch (err) {
      const apiErr = err as { code?: string; message?: string };
      const msg = apiErr?.code === 'Session.CannotCancel'
        ? 'No se puede cancelar una sesión que ya ha comenzado.'
        : (apiErr?.message ?? 'No se pudo cancelar la sesión.');
      window.alert(msg);
    } finally {
      setCancellingId(null);
    }
  }

  function startEdit(session: Session) {
    setEditingId(session.id);
    setEditError(null);
    setEditForm({
      name: session.name,
      scheduledAt: session.scheduledAt
        ? new Date(session.scheduledAt).toISOString().slice(0, 16)
        : null,
    });
  }

  function cancelEdit() {
    setEditingId(null);
    setEditError(null);
  }

  async function handleSaveEdit(e: React.FormEvent) {
    e.preventDefault();
    if (!editingId) return;
    setEditError(null);
    setSaving(true);
    try {
      await sessionService.update(editingId, editForm);
      setEditingId(null);
      await loadAll();
    } catch (err) {
      const apiErr = err as { code?: string; message?: string };
      if (apiErr?.code === 'Session.CannotEdit') {
        setEditError('No se puede modificar una sesión que ya ha comenzado.');
      } else {
        setEditError(apiErr?.message ?? 'No se pudo guardar la sesión.');
      }
    } finally {
      setSaving(false);
    }
  }

  const activeMissions = missions.filter(m => m.status === 'Active');

  return (
    <div>
      <PageHeader
        eyebrow="Operaciones"
        title="Sesiones"
        description={canManage
          ? 'Crea, edita y supervisa las instancias de juego. Solo las sesiones pendientes se pueden editar o cancelar.'
          : 'Consultá las instancias de juego y su detalle. La gestión está reservada al operador.'}
      />

      {/* ── Creación (solo en modo gestión — operador) ───────────────────── */}
      {canManage && (
      <Card className="mb-5">
        <CardHeader
          title="Nueva sesión"
          description="A partir de una misión activa generamos una sala de espera con su código de acceso."
        />
        <form onSubmit={handleCreate}>
          <Stack gap={3}>
            <FormField label="Misión (activa)" htmlFor="session-mission" required hint={
              activeMissions.length === 0 ? 'No hay misiones activas. Activá una desde la pestaña Misiones primero.' : undefined
            }>
              <Select
                id="session-mission"
                required
                value={form.missionId}
                onChange={e => setForm(f => ({ ...f, missionId: e.target.value }))}
                disabled={activeMissions.length === 0}
              >
                <option value="">— Seleccioná una misión —</option>
                {activeMissions.map(m => (
                  <option key={m.id} value={m.id}>{m.name}</option>
                ))}
              </Select>
            </FormField>

            <FormField label="Nombre de la sesión" htmlFor="session-name" required>
              <TextInput
                id="session-name"
                required
                value={form.name}
                onChange={e => setForm(f => ({ ...f, name: e.target.value }))}
                placeholder="Ej: Ronda 1 — Equipo A"
              />
            </FormField>

            <FormField label="Fecha programada (opcional)" htmlFor="session-scheduled">
              <TextInput
                id="session-scheduled"
                type="datetime-local"
                value={form.scheduledAt ?? ''}
                onChange={e => setForm(f => ({ ...f, scheduledAt: e.target.value || null }))}
              />
            </FormField>

            {createError && <Alert tone="danger">{createError}</Alert>}

            <div>
              <Button type="submit" disabled={creating || activeMissions.length === 0}>
                {creating ? 'Creando…' : 'Crear sesión'}
              </Button>
            </div>
          </Stack>
        </form>
      </Card>
      )}

      {/* ── Lista ─────────────────────────────────────────────────────────── */}
      {loading && <Card><Spinner label="Cargando sesiones…" /></Card>}

      {error && <Alert tone="danger">{error}</Alert>}

      {!loading && !error && sessions.length === 0 && (
        <Card>
          <EmptyState
            icon="🎮"
            title="Todavía no hay sesiones"
            description="Creá la primera completando el formulario de arriba."
          />
        </Card>
      )}

      {!loading && !error && sessions.length > 0 && (
        <Stack gap={3}>
          {sessions.map(session => {
            const missionName = missions.find(m => m.id === session.missionId)?.name ?? session.missionId;
            const isPending = session.status === 'Pending';
            const isEditing = editingId === session.id;
            const isCancelling = cancellingId === session.id;
            return (
              <Card key={session.id}>
                {!isEditing ? (
                  <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
                    <div className="min-w-0 flex-1">
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="text-base font-semibold text-ink">{session.name}</h3>
                        <StatusBadge status={session.status} />
                      </div>
                      <p className="text-sm text-ink-soft mt-1">
                        Misión: <span className="font-medium">{missionName}</span>
                      </p>
                      <p className="text-xs text-ink-muted mt-1">
                        Creada el {new Date(session.createdAt).toLocaleDateString('es-VE', { dateStyle: 'medium' })}
                        {session.scheduledAt && (
                          <> · Programada para {new Date(session.scheduledAt).toLocaleString('es-VE', { dateStyle: 'medium', timeStyle: 'short' })}</>
                        )}
                      </p>
                      {canManage && !isPending && (
                        <p className="text-xs text-ink-muted italic mt-1.5">
                          No se puede modificar una sesión que ya ha comenzado.
                        </p>
                      )}
                    </div>
                    <div className="flex items-center gap-2 shrink-0 flex-wrap">
                      {canManage && (
                        <>
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() => startEdit(session)}
                            disabled={!isPending || isCancelling}
                            title={isPending ? 'Editar sesión' : 'Solo se pueden editar sesiones pendientes'}
                          >
                            Editar
                          </Button>
                          <Button
                            variant="danger"
                            size="sm"
                            onClick={() => handleCancel(session)}
                            disabled={!isPending || isCancelling}
                            title={isPending ? 'Cancelar sesión' : 'Solo se pueden cancelar sesiones pendientes'}
                          >
                            {isCancelling ? 'Cancelando…' : 'Cancelar'}
                          </Button>
                        </>
                      )}
                      <Button size="sm" onClick={() => onViewDetail(session.id)}>
                        Ver detalle
                      </Button>
                    </div>
                  </div>
                ) : (
                  <form onSubmit={handleSaveEdit}>
                    <Stack gap={3}>
                      <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                        <FormField label="Nombre" htmlFor={`edit-name-${session.id}`} required>
                          <TextInput
                            id={`edit-name-${session.id}`}
                            required
                            autoFocus
                            value={editForm.name}
                            onChange={e => setEditForm(f => ({ ...f, name: e.target.value }))}
                          />
                        </FormField>
                        <FormField label="Fecha programada (opcional)" htmlFor={`edit-sched-${session.id}`}>
                          <TextInput
                            id={`edit-sched-${session.id}`}
                            type="datetime-local"
                            value={editForm.scheduledAt ?? ''}
                            onChange={e => setEditForm(f => ({ ...f, scheduledAt: e.target.value || null }))}
                          />
                        </FormField>
                      </div>
                      {editError && <Alert tone="danger">{editError}</Alert>}
                      <div className="flex items-center gap-2">
                        <Button type="submit" disabled={saving} size="sm">
                          {saving ? 'Guardando…' : 'Guardar'}
                        </Button>
                        <Button type="button" variant="secondary" size="sm" onClick={cancelEdit} disabled={saving}>
                          Cancelar
                        </Button>
                      </div>
                    </Stack>
                  </form>
                )}
              </Card>
            );
          })}
        </Stack>
      )}
    </div>
  );
}

// ── StatusBadge ───────────────────────────────────────────────────────────────

const STATUS_TONES: Record<SessionStatus, BadgeTone> = {
  Pending:    'warning',
  InProgress: 'brand',
  Paused:     'info',
  Completed:  'success',
  Cancelled:  'danger',
};

function StatusBadge({ status }: { status: SessionStatus }) {
  return (
    <Badge tone={STATUS_TONES[status] ?? 'neutral'}>
      {SESSION_STATUS_LABELS[status] ?? status}
    </Badge>
  );
}
