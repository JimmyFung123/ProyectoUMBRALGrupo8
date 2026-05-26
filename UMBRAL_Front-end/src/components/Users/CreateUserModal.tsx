import { useState } from 'react';
import { userService } from '../../services/userService';
import type { UserRole } from '../../types/user';

interface Props {
  onClose: () => void;
  onCreated: () => void | Promise<void>;
}

interface BackendError { code?: string; message?: string }

/**
 * HU-23 Criterio 1: registra un nuevo administrador u operador.
 * Las validaciones del backend (email único, formato) se reflejan en el banner
 * rojo. La regla del email duplicado devuelve 409 → mensaje específico del ERS:
 * "Este correo ya está en uso."
 */
export function CreateUserModal({ onClose, onCreated }: Props) {
  const [email, setEmail] = useState('');
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [temporaryPassword, setTemporaryPassword] = useState('');
  const [role, setRole] = useState<UserRole>('Operator');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const isValid =
    email.trim().length > 0 &&
    firstName.trim().length > 0 &&
    lastName.trim().length > 0 &&
    temporaryPassword.length >= 8;

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!isValid || submitting) return;

    setSubmitting(true);
    setError(null);

    try {
      await userService.create({
        email: email.trim().toLowerCase(),
        firstName: firstName.trim(),
        lastName: lastName.trim(),
        temporaryPassword,
        role,
      });
      await onCreated();
    } catch (err) {
      const be = err as BackendError | undefined;
      setError(be?.message ?? 'No se pudo crear el usuario.');
      setSubmitting(false);
    }
  }

  return (
    <div style={styles.overlay}>
      <form onSubmit={handleSubmit} style={styles.modal}>
        <header style={styles.header}>
          <h3 style={{ margin: 0, fontSize: '1.1rem' }}>➕ Nuevo usuario</h3>
          <button type="button" onClick={onClose} style={styles.closeBtn} aria-label="Cerrar">✕</button>
        </header>

        <label style={styles.field}>
          <span style={styles.label}>Correo electrónico *</span>
          <input
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="usuario@umbral.local"
            autoFocus
            required
            style={styles.input}
          />
        </label>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
          <label style={styles.field}>
            <span style={styles.label}>Nombre *</span>
            <input
              type="text"
              value={firstName}
              onChange={(e) => setFirstName(e.target.value)}
              required
              style={styles.input}
            />
          </label>
          <label style={styles.field}>
            <span style={styles.label}>Apellido *</span>
            <input
              type="text"
              value={lastName}
              onChange={(e) => setLastName(e.target.value)}
              required
              style={styles.input}
            />
          </label>
        </div>

        <label style={styles.field}>
          <span style={styles.label}>Contraseña temporal (mínimo 8 caracteres) *</span>
          <input
            type="text"
            value={temporaryPassword}
            onChange={(e) => setTemporaryPassword(e.target.value)}
            placeholder="ej. Umbral2026!"
            required
            minLength={8}
            style={{ ...styles.input, fontFamily: 'monospace' }}
          />
          <span style={styles.hint}>
            El usuario podrá usarla directamente. Para forzar cambio al primer login,
            ajustá el realm JSON.
          </span>
        </label>

        <label style={styles.field}>
          <span style={styles.label}>Rol *</span>
          <select
            value={role}
            onChange={(e) => setRole(e.target.value as UserRole)}
            style={styles.input}
          >
            <option value="Operator">Operador — gestiona sesiones en vivo</option>
            <option value="Admin">Administrador — todo lo anterior + gestión de personal y misiones</option>
          </select>
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
            disabled={!isValid || submitting}
            style={{
              ...styles.submitBtn,
              opacity: !isValid || submitting ? 0.5 : 1,
              cursor: !isValid || submitting ? 'not-allowed' : 'pointer',
            }}
          >
            {submitting ? 'Creando…' : 'Crear usuario'}
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
    minWidth: 420, maxWidth: 560, width: '90%',
    boxShadow: '0 4px 24px rgba(0,0,0,0.2)',
    display: 'flex', flexDirection: 'column', gap: '0.75rem',
  },
  header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center' },
  closeBtn: { background: 'none', border: 'none', fontSize: '1.2rem', cursor: 'pointer', color: '#666' },
  field: { display: 'flex', flexDirection: 'column', gap: '0.3rem' },
  label: { fontSize: '0.85rem', fontWeight: 600, color: '#374151' },
  input: {
    padding: '0.5rem 0.7rem', fontSize: '0.9rem',
    border: '1px solid #ccc', borderRadius: 4,
    boxSizing: 'border-box',
  },
  hint: { fontSize: '0.72rem', color: '#999' },
  errorBanner: {
    background: '#fee2e2', border: '1px solid #fca5a5',
    color: '#7f1d1d', padding: '0.6rem 0.9rem',
    borderRadius: 6, fontSize: '0.85rem',
  },
  footer: {
    display: 'flex', gap: '0.5rem', justifyContent: 'flex-end',
    marginTop: '0.25rem',
  },
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
