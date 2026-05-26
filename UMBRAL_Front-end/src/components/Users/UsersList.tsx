import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '../../auth/AuthProvider';
import { userService } from '../../services/userService';
import type { UmbralUserRow } from '../../types/user';
import { ChangeRoleModal } from './ChangeRoleModal';
import { CreateUserModal } from './CreateUserModal';

interface BackendError { code?: string; message?: string }

function errorMessage(err: unknown, fallback: string): string {
  const be = err as BackendError | undefined;
  return be?.message ?? fallback;
}

/**
 * HU-23 — Gestión integral de personal operativo.
 *
 * Tabla con todos los usuarios del realm (admins primero, después operadores).
 * Acciones por fila: cambiar rol, deshabilitar / habilitar.
 * Los botones se ocultan en la fila del propio usuario para no romper la regla
 * "no auto-desactivarse" antes de pegarle al backend.
 *
 * Toda esta pantalla solo se monta para administradores — App.jsx oculta la
 * pestaña por completo si `isAdmin === false`. Pero por defensa-en-profundidad,
 * el backend valida `[Authorize(Roles=admin)]` en todos los endpoints.
 */
export function UsersList() {
  const { user: me } = useAuth();
  const [users, setUsers] = useState<UmbralUserRow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [changingRoleFor, setChangingRoleFor] = useState<UmbralUserRow | null>(null);
  const [pendingId, setPendingId] = useState<string | null>(null);

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

  async function withRowLoading(id: string, fn: () => Promise<void>) {
    setPendingId(id);
    setError(null);
    try {
      await fn();
      await load();
    } catch (err) {
      setError(errorMessage(err, 'La acción no se pudo completar.'));
    } finally {
      setPendingId(null);
    }
  }

  if (loading) {
    return <p style={styles.muted}>Cargando personal…</p>;
  }

  return (
    <div style={{ maxWidth: 1000, margin: '0 auto', padding: '1.5rem', fontFamily: 'sans-serif' }}>
      <div style={styles.header}>
        <div>
          <h1 style={styles.title}>👥 Personal operativo</h1>
          <p style={styles.subtitle}>
            {users.length} {users.length === 1 ? 'cuenta registrada' : 'cuentas registradas'}.
            Los cambios se reflejan al volver a iniciar sesión.
          </p>
        </div>
        <button onClick={() => setShowCreate(true)} style={styles.newBtn}>
          ➕ Nuevo usuario
        </button>
      </div>

      {error && (
        <div style={styles.errorBanner}>
          ⚠ {error}
          <button onClick={() => setError(null)} style={styles.errorClose}>✕</button>
        </div>
      )}

      {users.length === 0 ? (
        <div style={styles.emptyBox}>
          <p style={{ margin: 0 }}>Aún no hay personal registrado.</p>
        </div>
      ) : (
        <table style={styles.table}>
          <thead>
            <tr style={{ background: '#f5f5f5' }}>
              <th style={styles.th}>Nombre</th>
              <th style={styles.th}>Correo</th>
              <th style={{ ...styles.th, textAlign: 'center' }}>Rol</th>
              <th style={{ ...styles.th, textAlign: 'center' }}>Estado</th>
              <th style={{ ...styles.th, textAlign: 'center' }}>Acciones</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => {
              const isMe = me?.id === u.id;
              const isAdmin = u.role === 'admin';
              const isPending = pendingId === u.id;
              const fullName = `${u.firstName} ${u.lastName}`.trim() || '—';

              return (
                <tr key={u.id} style={{
                  borderBottom: '1px solid #eee',
                  background: isMe ? '#eef2ff' : undefined,
                  opacity: u.enabled ? 1 : 0.55,
                }}>
                  <td style={styles.td}>
                    <strong>{fullName}</strong>
                    {isMe && <span style={styles.youTag}>tú</span>}
                  </td>
                  <td style={{ ...styles.td, fontFamily: 'monospace', fontSize: '0.85rem', color: '#666' }}>
                    {u.email}
                  </td>
                  <td style={{ ...styles.td, textAlign: 'center' }}>
                    <RoleBadge role={u.role} />
                  </td>
                  <td style={{ ...styles.td, textAlign: 'center' }}>
                    <StatusBadge enabled={u.enabled} />
                  </td>
                  <td style={{ ...styles.td, textAlign: 'center' }}>
                    <button
                      onClick={() => setChangingRoleFor(u)}
                      disabled={isPending || isMe}
                      title={isMe ? 'No puedes cambiar tu propio rol.' : 'Cambiar rol del usuario'}
                      style={{
                        ...styles.actionBtn,
                        cursor: isPending || isMe ? 'not-allowed' : 'pointer',
                        opacity: isPending || isMe ? 0.4 : 1,
                      }}
                    >
                      🔄 Cambiar rol
                    </button>
                    {u.enabled ? (
                      <button
                        onClick={() => withRowLoading(u.id, () => userService.disable(u.id))}
                        disabled={isPending || isMe}
                        title={isMe ? 'No puedes deshabilitar tu propia cuenta.' : 'Deshabilitar al usuario (no se borra)'}
                        style={{
                          ...styles.actionBtn,
                          marginLeft: '0.35rem',
                          background: '#fee2e2',
                          borderColor: '#fca5a5',
                          color: '#991b1b',
                          cursor: isPending || isMe ? 'not-allowed' : 'pointer',
                          opacity: isPending || isMe ? 0.4 : 1,
                        }}
                      >
                        {isPending ? '…' : '🚫 Deshabilitar'}
                      </button>
                    ) : (
                      <button
                        onClick={() => withRowLoading(u.id, () => userService.enable(u.id))}
                        disabled={isPending}
                        style={{
                          ...styles.actionBtn,
                          marginLeft: '0.35rem',
                          background: '#dcfce7',
                          borderColor: '#86efac',
                          color: '#166534',
                          cursor: isPending ? 'not-allowed' : 'pointer',
                          opacity: isPending ? 0.4 : 1,
                        }}
                      >
                        {isPending ? '…' : '✅ Habilitar'}
                      </button>
                    )}
                    {!u.role && (
                      <span style={{ marginLeft: '0.5rem', fontSize: '0.7rem', color: '#dc2626' }}>
                        ⚠ Sin rol UMBRAL
                      </span>
                    )}
                    {isAdmin && !isMe && (
                      <span style={{ marginLeft: '0.5rem', fontSize: '0.7rem', color: '#6b7280' }}>
                        {/* recordatorio del Criterio 4 */}
                      </span>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      )}

      {showCreate && (
        <CreateUserModal
          onClose={() => setShowCreate(false)}
          onCreated={async () => {
            setShowCreate(false);
            await load();
          }}
        />
      )}

      {changingRoleFor && (
        <ChangeRoleModal
          user={changingRoleFor}
          onClose={() => setChangingRoleFor(null)}
          onChanged={async () => {
            setChangingRoleFor(null);
            await load();
          }}
        />
      )}
    </div>
  );
}

// ── Sub-componentes ─────────────────────────────────────────────────────────

function RoleBadge({ role }: { role: string | null }) {
  if (role === 'admin') {
    return <span style={{ ...styles.pill, background: '#3730a3', color: '#fff' }}>Administrador</span>;
  }
  if (role === 'operator') {
    return <span style={{ ...styles.pill, background: '#0e7490', color: '#fff' }}>Operador</span>;
  }
  return <span style={{ ...styles.pill, background: '#9ca3af', color: '#fff' }}>Sin rol</span>;
}

function StatusBadge({ enabled }: { enabled: boolean }) {
  return enabled
    ? <span style={{ ...styles.pill, background: '#dcfce7', color: '#166534' }}>Activo</span>
    : <span style={{ ...styles.pill, background: '#fee2e2', color: '#991b1b' }}>Inactivo</span>;
}

// ── Estilos ─────────────────────────────────────────────────────────────────

const styles: Record<string, React.CSSProperties> = {
  header: {
    display: 'flex',
    alignItems: 'flex-start',
    justifyContent: 'space-between',
    gap: '1rem',
    marginBottom: '1rem',
    flexWrap: 'wrap',
  },
  title: { margin: '0 0 0.25rem', fontSize: '1.5rem' },
  subtitle: { margin: 0, color: '#666', fontSize: '0.85rem' },
  newBtn: {
    padding: '0.55rem 1rem',
    background: '#6366f1',
    color: '#fff',
    border: 'none',
    borderRadius: 6,
    fontSize: '0.9rem',
    fontWeight: 600,
    cursor: 'pointer',
  },
  errorBanner: {
    background: '#fee2e2',
    border: '1px solid #fca5a5',
    color: '#7f1d1d',
    padding: '0.6rem 0.9rem',
    borderRadius: 6,
    fontSize: '0.88rem',
    marginBottom: '0.75rem',
    display: 'flex',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  errorClose: {
    background: 'none', border: 'none', cursor: 'pointer',
    color: '#7f1d1d', fontWeight: 'bold', fontSize: '1rem',
  },
  emptyBox: {
    padding: '2rem',
    background: '#fafafa',
    border: '1px dashed #ccc',
    borderRadius: 6,
    textAlign: 'center',
    color: '#888',
  },
  table: { width: '100%', borderCollapse: 'collapse', fontSize: '0.9rem' },
  th: {
    padding: '0.55rem 0.75rem',
    fontWeight: 600,
    fontSize: '0.8rem',
    color: '#555',
    borderBottom: '2px solid #ddd',
    textAlign: 'left',
  },
  td: { padding: '0.6rem 0.75rem', verticalAlign: 'middle' },
  pill: {
    display: 'inline-block',
    fontSize: '0.72rem',
    fontWeight: 700,
    padding: '0.18rem 0.6rem',
    borderRadius: 9999,
    textTransform: 'uppercase',
    letterSpacing: '0.04em',
  },
  actionBtn: {
    padding: '0.25rem 0.7rem',
    fontSize: '0.78rem',
    background: '#fff',
    border: '1px solid #ccc',
    borderRadius: 4,
    fontWeight: 600,
  },
  youTag: {
    background: '#6366f1',
    color: '#fff',
    fontSize: '0.62rem',
    padding: '0.1rem 0.4rem',
    borderRadius: 9999,
    fontWeight: 700,
    textTransform: 'uppercase',
    marginLeft: '0.4rem',
    letterSpacing: '0.05em',
  },
  muted: { color: '#999', fontSize: '0.9rem', textAlign: 'center', padding: '2rem' },
};
