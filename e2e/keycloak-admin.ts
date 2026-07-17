import {
  KEYCLOAK_URL,
  KC_ADMIN_USER,
  KC_ADMIN_PASSWORD,
  KC_REALM,
  OPERATOR_USER,
  OPERATOR_PASSWORD,
  OPERATOR_B_USER,
  OPERATOR_B_PASSWORD,
} from './env';

/**
 * Aprovisiona el usuario operador de E2E directamente contra la Admin REST API
 * de Keycloak (realm master, admin/admin de docker-compose.yml).
 *
 * ¿Por qué crearlo por API y no por la UI de "Personal"? Porque esa pantalla
 * (HU-23) genera una contraseña temporal aleatoria y la envía por correo, y
 * además exige cambiarla en el primer ingreso (UPDATE_PASSWORD). Nada de eso es
 * determinista para un test. Aquí lo dejamos con contraseña PERMANENTE, correo
 * verificado y sin acciones pendientes, de modo que el login por formulario del
 * global-setup entre directo.
 *
 * Es idempotente: si el usuario ya existe, solo resetea su contraseña, limpia
 * las acciones pendientes y (re)asigna el rol `operator`.
 */

const ADMIN_BASE = `${KEYCLOAK_URL}/admin/realms/${KC_REALM}`;

async function getAdminToken(): Promise<string> {
  const res = await fetch(
    `${KEYCLOAK_URL}/realms/master/protocol/openid-connect/token`,
    {
      method: 'POST',
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
      body: new URLSearchParams({
        username: KC_ADMIN_USER,
        password: KC_ADMIN_PASSWORD,
        grant_type: 'password',
        client_id: 'admin-cli',
      }),
    },
  );
  if (!res.ok) {
    throw new Error(
      `No se pudo autenticar contra Keycloak master (${res.status}). ` +
        `¿Está el stack levantado y KEYCLOAK_ADMIN=${KC_ADMIN_USER}?`,
    );
  }
  const json = (await res.json()) as { access_token?: string };
  if (!json.access_token) throw new Error('Keycloak no devolvió access_token.');
  return json.access_token;
}

interface AuthHeaders {
  Authorization: string;
  'Content-Type': string;
}

async function findUserId(headers: AuthHeaders, username: string): Promise<string | null> {
  const url = `${ADMIN_BASE}/users?exact=true&username=${encodeURIComponent(username)}`;
  const res = await fetch(url, { headers });
  if (!res.ok) throw new Error(`GET users falló (${res.status}).`);
  const users = (await res.json()) as Array<{ id: string }>;
  return users[0]?.id ?? null;
}

async function createUser(
  headers: AuthHeaders, username: string, firstName: string, lastName: string,
): Promise<string> {
  const res = await fetch(`${ADMIN_BASE}/users`, {
    method: 'POST',
    headers,
    body: JSON.stringify({
      username,
      email: username,
      firstName,
      lastName,
      enabled: true,
      emailVerified: true,
      requiredActions: [],
    }),
  });
  // 201 → creado; 409 → carrera con otra corrida, lo resolvemos con un GET.
  if (res.status !== 201 && res.status !== 409) {
    throw new Error(`POST users falló (${res.status}): ${await res.text()}`);
  }
  const id = await findUserId(headers, username);
  if (!id) throw new Error(`El usuario ${username} no aparece tras crearlo.`);
  return id;
}

async function resetPassword(headers: AuthHeaders, userId: string, password: string): Promise<void> {
  const res = await fetch(`${ADMIN_BASE}/users/${userId}/reset-password`, {
    method: 'PUT',
    headers,
    body: JSON.stringify({ type: 'password', value: password, temporary: false }),
  });
  if (!res.ok) throw new Error(`reset-password falló (${res.status}).`);
}

async function clearRequiredActions(headers: AuthHeaders, userId: string): Promise<void> {
  const res = await fetch(`${ADMIN_BASE}/users/${userId}`, {
    method: 'PUT',
    headers,
    body: JSON.stringify({ enabled: true, emailVerified: true, requiredActions: [] }),
  });
  if (!res.ok) throw new Error(`PUT user falló (${res.status}).`);
}

async function assignOperatorRole(headers: AuthHeaders, userId: string): Promise<void> {
  const roleRes = await fetch(`${ADMIN_BASE}/roles/operator`, { headers });
  if (!roleRes.ok) {
    throw new Error(
      `No existe el rol realm 'operator' (${roleRes.status}). ` +
        `Revisá scripts/keycloak/umbral-realm.json.`,
    );
  }
  const role = (await roleRes.json()) as { id: string; name: string };
  // Asignar un rol ya asignado es idempotente en Keycloak (204).
  const res = await fetch(`${ADMIN_BASE}/users/${userId}/role-mappings/realm`, {
    method: 'POST',
    headers,
    body: JSON.stringify([{ id: role.id, name: role.name }]),
  });
  if (!res.ok && res.status !== 409) {
    throw new Error(`Asignar rol operator falló (${res.status}).`);
  }
}

/** Crea o normaliza un operador de E2E dado. Devuelve su id de Keycloak. */
async function ensureOperator(
  username: string, password: string, firstName: string, lastName: string,
): Promise<string> {
  const token = await getAdminToken();
  const headers: AuthHeaders = {
    Authorization: `Bearer ${token}`,
    'Content-Type': 'application/json',
  };

  const userId = (await findUserId(headers, username)) ?? (await createUser(headers, username, firstName, lastName));
  await clearRequiredActions(headers, userId);
  await resetPassword(headers, userId, password);
  await assignOperatorRole(headers, userId);
  return userId;
}

/** Crea o normaliza al operador principal de E2E. */
export async function ensureOperatorUser(): Promise<string> {
  return ensureOperator(OPERATOR_USER, OPERATOR_PASSWORD, 'Operador', 'E2E');
}

/**
 * Segundo operador de E2E — RB-10: una sesión solo la administra quien la
 * creó, así que el test necesita dos identidades reales y distintas.
 */
export async function ensureOperatorBUser(): Promise<string> {
  return ensureOperator(OPERATOR_B_USER, OPERATOR_B_PASSWORD, 'Operador', 'B E2E');
}
