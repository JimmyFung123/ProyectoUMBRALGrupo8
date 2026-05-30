import keycloak from '../auth/keycloak';

/**
 * Wrapper de fetch para el front operador (HU-23).
 *
 * Adjunta `Authorization: Bearer <access_token>` en TODAS las llamadas a la
 * API y refresca el token automáticamente si está a 30s de expirar. Reemplaza
 * el header legacy `X-Operator-Name` que usábamos antes de tener Keycloak —
 * el backend ahora deriva el actor del JWT.
 *
 * Si la respuesta es 401, asume que el refresh token también caducó y dispara
 * un nuevo login. Si es 403, lo propaga: el caller decide cómo manejarlo
 * (la UI muestra un mensaje "No tienes permisos" en general).
 */
async function ensureFreshToken(): Promise<string | null> {
  if (!keycloak.authenticated) return null;
  try {
    await keycloak.updateToken(30);
  } catch {
    keycloak.login();
    return null;
  }
  return keycloak.token ?? null;
}

async function authedFetch(input: RequestInfo, init: RequestInit = {}): Promise<Response> {
  const token = await ensureFreshToken();
  const headers = new Headers(init.headers);
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const response = await fetch(input, { ...init, headers });

  // Un 401 CUANDO ya tenemos sesión válida significa que el back rechazó un
  // token que no debería (o la llamada se adelantó a la auth). Re-loguear acá
  // rebotaría por el SSO de Keycloak (aún válido) y volvería al instante: un
  // loop de redirección infinito. Solo forzamos login si NO hay sesión; si la
  // hay, dejamos que el error se propague al caller.
  if (response.status === 401 && !keycloak.authenticated) {
    keycloak.login();
  }
  return response;
}

export async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({
      code: 'Unknown',
      message: response.statusText,
    }));
    throw error;
  }
  if (response.status === 204) return undefined as T;
  return response.json() as Promise<T>;
}

export const http = {
  get<T>(url: string): Promise<T> {
    return authedFetch(url, { method: 'GET' }).then(handleResponse<T>);
  },
  post<T>(url: string, body?: unknown): Promise<T> {
    return authedFetch(url, {
      method: 'POST',
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }).then(handleResponse<T>);
  },
  put<T>(url: string, body?: unknown): Promise<T> {
    return authedFetch(url, {
      method: 'PUT',
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }).then(handleResponse<T>);
  },
  patch<T>(url: string, body?: unknown): Promise<T> {
    return authedFetch(url, {
      method: 'PATCH',
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }).then(handleResponse<T>);
  },
  delete<T>(url: string): Promise<T> {
    return authedFetch(url, { method: 'DELETE' }).then(handleResponse<T>);
  },
};
