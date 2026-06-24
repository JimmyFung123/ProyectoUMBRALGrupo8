import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../../auth/AuthProvider';
import { userService } from '../../services/userService';
import type { UmbralUserRow } from '../../types/user';
import { ChangeRoleModal } from './ChangeRoleModal';
import { CreateUserModal } from './CreateUserModal';
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  PageHeader,
  Spinner,
  type BadgeTone,
} from '../ui';

interface BackendError { code?: string; message?: string }

function errorMessage(err: unknown, fallback: string): string {
  const be = err as BackendError | undefined;
  return be?.message ?? fallback;
}

/**
 * HU-23 — Gestión integral de personal operativo.
 */
export function UsersList() {
  const { user: me } = useAuth();
  const [users, setUsers] = useState<UmbralUserRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [changingRoleFor, setChangingRoleFor] = useState<UmbralUserRow | null>(null);
  const [pendingId, setPendingId] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      setError(null);
      const list = await userService.list();
      setUsers(list);
    } catch (err) {
      setError(errorMessage(err, 'No se pudo cargar la lista de personal.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { void load(); }, [load]);

  async function withRowLoading(id: string, fn: () => Promise<void>, successMsg?: string) {
    setPendingId(id);
    setError(null);
    setSuccess(null);
    try {
      await fn();
      if (successMsg) setSuccess(successMsg);
      else await load();
    } catch (err) {
      setError(errorMessage(err, 'La acción no se pudo completar.'));
    } finally {
      setPendingId(null);
    }
  }

  if (loading) return <Card><Spinner label="Cargando personal…" /></Card>;

  return (
    <div>
      <PageHeader
        eyebrow="Administración"
        title="👥 Personal operativo"
        description={`${users.length} ${users.length === 1 ? 'cuenta registrada' : 'cuentas registradas'}. Los cambios se reflejan al volver a iniciar sesión.`}
        actions={
          <Button onClick={() => setShowCreate(true)} leadingIcon="➕">
            Nuevo usuario
          </Button>
        }
      />

      {error && <Alert tone="danger" onDismiss={() => setError(null)} className="mb-4">{error}</Alert>}
      {success && <Alert tone="success" onDismiss={() => setSuccess(null)} className="mb-4">{success}</Alert>}

      {users.length === 0 ? (
        <Card>
          <EmptyState
            icon="👥"
            title="Aún no hay personal registrado"
            description="Creá la primera cuenta con el botón de arriba."
          />
        </Card>
      ) : (
        <Card padded={false}>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="bg-surface-subtle">
                <tr>
                  <Th>Nombre</Th>
                  <Th>Correo</Th>
                  <Th align="center">Rol</Th>
                  <Th align="center">Estado</Th>
                  <Th align="center">Acciones</Th>
                </tr>
              </thead>
              <tbody>
                {users.map(u => {
                  const isMe = me?.id === u.id;
                  const isPending = pendingId === u.id;
                  const fullName = `${u.firstName} ${u.lastName}`.trim() || '—';
                  return (
                    <tr
                      key={u.id}
                      className={[
                        'border-t border-slate-100',
                        isMe ? 'bg-brand-50/60' : '',
                        !u.enabled ? 'opacity-60' : '',
                      ].filter(Boolean).join(' ')}
                    >
                      <Td>
                        <div className="flex items-center gap-2">
                          <span className="font-semibold text-ink">{fullName}</span>
                          {isMe && (
                            <Badge tone="brand" variant="solid">tú</Badge>
                          )}
                        </div>
                      </Td>
                      <Td>
                        <span className="font-mono text-xs text-ink-muted">{u.email}</span>
                      </Td>
                      <Td align="center"><RoleBadge role={u.role} /></Td>
                      <Td align="center"><StatusPill enabled={u.enabled} /></Td>
                      <Td align="center">
                        <div className="flex items-center gap-2 justify-center flex-wrap">
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() => setChangingRoleFor(u)}
                            disabled={isPending || isMe}
                            title={isMe ? 'No puedes cambiar tu propio rol.' : 'Cambiar rol del usuario'}
                          >
                            🔄 Cambiar rol
                          </Button>
                          <Button
                            variant="secondary"
                            size="sm"
                            onClick={() => withRowLoading(
                              u.id,
                              () => userService.sendTemporaryPassword(u.id),
                              `Clave temporal enviada a ${u.email}.`
                            )}
                            disabled={isPending}
                            title="Genera una nueva clave temporal y la envía al correo del usuario"
                          >
                            {isPending ? '…' : '🔑 Enviar clave'}
                          </Button>
                          {u.enabled ? (
                            <Button
                              variant="danger"
                              size="sm"
                              onClick={() => withRowLoading(u.id, () => userService.disable(u.id))}
                              disabled={isPending || isMe}
                              title={isMe ? 'No puedes deshabilitar tu propia cuenta.' : 'Deshabilitar al usuario (no se borra)'}
                            >
                              {isPending ? '…' : '🚫 Deshabilitar'}
                            </Button>
                          ) : (
                            <Button
                              variant="primary"
                              size="sm"
                              onClick={() => withRowLoading(u.id, () => userService.enable(u.id))}
                              disabled={isPending}
                            >
                              {isPending ? '…' : '✅ Habilitar'}
                            </Button>
                          )}
                        </div>
                      </Td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </Card>
      )}

      {showCreate && (
        <CreateUserModal
          onClose={() => setShowCreate(false)}
          onCreated={async () => { setShowCreate(false); await load(); }}
        />
      )}

      {changingRoleFor && (
        <ChangeRoleModal
          user={changingRoleFor}
          onClose={() => setChangingRoleFor(null)}
          onChanged={async () => { setChangingRoleFor(null); await load(); }}
        />
      )}
    </div>
  );
}

// ── Tiny table primitives ─────────────────────────────────────────────────────

function Th({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'center' | 'right' }) {
  return (
    <th
      className={`px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-ink-muted text-${align}`}
    >
      {children}
    </th>
  );
}

function Td({ children, align = 'left' }: { children: React.ReactNode; align?: 'left' | 'center' | 'right' }) {
  return (
    <td className={`px-3 py-2.5 align-middle text-${align}`}>{children}</td>
  );
}

// ── Sub-componentes ───────────────────────────────────────────────────────────

const ROLE_TONE: Record<string, BadgeTone> = {
  admin: 'brand',
  operator: 'info',
};

function RoleBadge({ role }: { role: string | null }) {
  if (!role) return <Badge tone="neutral" variant="solid">Sin rol</Badge>;
  const tone = ROLE_TONE[role] ?? 'neutral';
  const label = role === 'admin' ? 'Administrador' : role === 'operator' ? 'Operador' : role;
  return <Badge tone={tone} variant="solid">{label}</Badge>;
}

function StatusPill({ enabled }: { enabled: boolean }) {
  return enabled
    ? <Badge tone="success">Activo</Badge>
    : <Badge tone="danger">Inactivo</Badge>;
}
