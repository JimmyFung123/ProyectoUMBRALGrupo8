/** Roles que el realm UMBRAL emite (HU-23). El backend acepta string. */
export type UserRole = 'Admin' | 'Operator';

export interface UmbralUserRow {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  /** Lowercase del Keycloak: "admin", "operator" o null cuando aún no tiene rol UMBRAL. */
  role: string | null;
  enabled: boolean;
}

export interface CreateUserPayload {
  email: string;
  firstName: string;
  lastName: string;
  role: UserRole;
}
