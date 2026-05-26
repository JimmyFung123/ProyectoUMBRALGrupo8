import { useAuth } from '../auth/AuthProvider';

/**
 * HU-23: barra superior que ahora muestra al usuario autenticado vía Keycloak.
 * Reemplaza el flujo manual con localStorage que usábamos en HU-22 — el nombre
 * y el rol vienen directamente del JWT y no se pueden falsificar desde el front.
 */
export function OperatorIdentityBar() {
  const { user, isAdmin, logout } = useAuth();
  if (!user) return null;

  const roleLabel = isAdmin ? 'Administrador' : 'Operador';
  const roleColor = isAdmin ? '#3730a3' : '#0e7490';

  return (
    <div style={styles.bar}>
      <span style={styles.label}>👤 {user.name}</span>
      <span style={{ ...styles.rolePill, background: roleColor }}>{roleLabel}</span>
      <span style={styles.email}>{user.email}</span>
      <button onClick={logout} style={styles.logoutBtn} title="Cerrar sesión y volver al login de Keycloak">
        Cerrar sesión
      </button>
    </div>
  );
}

const styles: Record<string, React.CSSProperties> = {
  bar: {
    display: 'flex',
    alignItems: 'center',
    gap: '0.75rem',
    padding: '0.45rem 1rem',
    background: '#eef2ff',
    borderBottom: '1px solid #c7d2fe',
    fontSize: '0.85rem',
    flexWrap: 'wrap',
  },
  label: { color: '#4338ca', fontWeight: 600 },
  rolePill: {
    color: '#fff',
    fontSize: '0.7rem',
    fontWeight: 700,
    padding: '0.15rem 0.55rem',
    borderRadius: 9999,
    textTransform: 'uppercase',
    letterSpacing: '0.04em',
  },
  email: {
    color: '#6b7280',
    fontSize: '0.78rem',
    fontFamily: 'monospace',
  },
  logoutBtn: {
    marginLeft: 'auto',
    background: 'transparent',
    border: '1px solid #6366f1',
    color: '#6366f1',
    fontSize: '0.78rem',
    fontWeight: 600,
    padding: '0.25rem 0.7rem',
    borderRadius: 4,
    cursor: 'pointer',
  },
};
