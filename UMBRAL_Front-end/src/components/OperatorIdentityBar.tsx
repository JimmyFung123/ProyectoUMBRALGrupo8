import { useAuth } from '../auth/AuthProvider';
import { Badge, Button } from './ui';

/**
 * HU-23: barra superior que ahora muestra al usuario autenticado vía Keycloak.
 * Reemplaza el flujo manual con localStorage que usábamos en HU-22 — el nombre
 * y el rol vienen directamente del JWT y no se pueden falsificar desde el front.
 */
export function OperatorIdentityBar() {
  const { user, isAdmin, logout } = useAuth();
  if (!user) return null;

  return (
    <div className="flex items-center gap-3 px-4 py-2 bg-brand-50 border-b border-brand-100 text-sm flex-wrap">
      <div className="flex items-center gap-2 min-w-0">
        <span className="text-brand-700 font-semibold truncate">👤 {user.name}</span>
        <Badge tone={isAdmin ? 'brand' : 'info'} variant="solid">
          {isAdmin ? 'Administrador' : 'Operador'}
        </Badge>
      </div>
      <span className="text-ink-muted text-xs font-mono truncate">{user.email}</span>
      <div className="ml-auto">
        <Button
          variant="ghost"
          size="sm"
          onClick={logout}
          title="Cerrar sesión y volver al login de Keycloak"
        >
          Cerrar sesión
        </Button>
      </div>
    </div>
  );
}
