import { useState } from 'react';
import { userService } from '../../services/userService';
import type { UmbralUserRow, UserRole } from '../../types/user';

interface Props {
  user: UmbralUserRow;
  onClose: () => void;
  onChanged: () => void | Promise<void>;
}

interface BackendError { code?: string; message?: string }

/**
 * HU-23 Criterio 2: cambiar el rol entre Administrador y Operador.
 *
 * El backend valida HU-23 Criterio 4 (no degradar al único administrador
 * activo) y devuelve un mensaje en español que mostramos tal cual.
 */
export function ChangeRoleModal({ user, onClose, onChanged }: Props) {
  const currentRole: UserRole | null =
    user.role === 'admin' ? 'Admin' :
    user.role === 'operator' ? 'Operator' :
    null;
  const initialTarget: UserRole = currentRole === 'Admin' ? 'Operator' : 'Admin';
  const [newRole, setNewRole] = useState<UserRole>(initialTarget);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isSameAsCurrent = currentRole !== null && currentRole === newRole;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (submitting || isSameAsCurrent) return;

    setSubmitting(true);
    setError(null);

    try {
      await userService.changeRole(user.id, newRole);
      await onChanged();
    } catch (err) {
      const be = err as BackendError | undefined;
      setError(be?.message ?? 'No se pudo cambiar el rol.');
      setSubmitting(false);
    }
  }

  const fullName = `${user.firstName} ${user.lastName}`.trim() || user.email;

  return (
    <div style={styles.overlay}>
      <form onSubmit={handleSubmit} style={styles.modal}>
        <header style={styles.header}>
          <h3 style={{ margin: 0, fontSize: '1.1rem' }}>🔄 Cambiar rol</h3>
          <button type="button" onClick={onClose} style={styles.closeBtn} aria-label="Cerrar">✕</button>
        </header>

        <p style={styles.userLine}>
          <strong>{fullName}</strong>
          <br />
          <span style={{ color: '#666', fontSize: '0.85rem', fontFamily: 'monospace' }}>{user.email}</span>
        </p>

        <div style={styles.field}>
          <span style={styles.label}>Rol actual</span>
          <div style={styles.currentRolePill}>
            {currentRole === 'Admin' ? 'Administrador' : currentRole === 'Operator' ? 'Operador' : 'Sin rol UMBRAL'}
          </div>
        </div>

        <label style={styles.field}>
          <span style={styles.label}>Nuevo rol *</span>
          <select
            value={newRole}
            onChange={(e) => setNewRole(e.target.value as UserRole)}
            style={styles.input}
          >
            <option value="Operator">Operador</option>
            <option value="Admin">Administrador</option>
          </select>
          {isSameAsCurrent && (
            <span style={styles.hint}>
              Este usuario ya tiene ese rol — no hay nada que cambiar.
            </span>
          )}
        </label>

        {error && (
          <div style={styles.errorBanner}>⚠ {error}</div>
        )}

        <footer style={styles.footer}>
          <button type="button" onClick={onClose} disabled={submitting} style={styles.cancelBtn}>
            Cancelar
          </button>
          <button
            type="submit"
            disabled={submitting || isSameAsCurrent}
            style={{
              ...styles.submitBtn,
              opacity: submitting || isSameAsCurrent ? 0.5 : 1,
              cursor: submitting || isSameAsCurrent ? 'not-allowed' : 'pointer',
            }}
          >
            {submitting ? 'Guardando…' : 'Aplicar cambio'}
          </button>
        </footer>
      </form>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  overlay: {
    position: 'fixed', inset: 0,
    background: 'rgba(0,0,0,0.45)',
    display: 'flex', alignItems: 'center', justifyContent: 'center',
    zIndex: 1000,
  },
  modal: {
    background: '#fff', borderRadius: 8, padding: '1.5rem',
    minWidth: 380, maxWidth: 460, width: '90%',
    boxShadow: '0 4px 24px rgba(0,0,0,0.2)',
    display: 'flex', flexDirection: 'column', gap: '0.85rem',
  },
  header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center' },
  closeBtn: { background: 'none', border: 'none', fontSize: '1.2rem', cursor: 'pointer', color: '#666' },
  userLine: { margin: 0, fontSize: '0.95rem' },
  field: { display: 'flex', flexDirection: 'column', gap: '0.3rem' },
  label: { fontSize: '0.8rem', fontWeight: 600, color: '#374151', textTransform: 'uppercase', letterSpacing: '0.04em' },
  currentRolePill: {
    display: 'inline-block',
    padding: '0.3rem 0.8rem',
    background: '#f3f4f6',
    border: '1px solid #d1d5db',
    borderRadius: 4,
    fontSize: '0.88rem',
    color: '#374151',
    alignSelf: 'flex-start',
  },
  input: {
    padding: '0.5rem 0.7rem', fontSize: '0.9rem',
    border: '1px solid #ccc', borderRadius: 4,
    boxSizing: 'border-box',
  },
  hint: { fontSize: '0.78rem', color: '#999', fontStyle: 'italic' },
  errorBanner: {
    background: '#fee2e2', border: '1px solid #fca5a5',
    color: '#7f1d1d', padding: '0.6rem 0.9rem',
    borderRadius: 6, fontSize: '0.85rem',
  },
  footer: { display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' },
  cancelBtn: {
    padding: '0.5rem 1rem', cursor: 'pointer',
    borderRadius: 4, border: '1px solid #ccc',
    background: '#fff', fontSize: '0.88rem',
  },
  submitBtn: {
    padding: '0.5rem 1rem',
    borderRadius: 4, border: 'none',
    background: '#6366f1', color: '#fff',
    fontWeight: 600, fontSize: '0.88rem',
  },
};
