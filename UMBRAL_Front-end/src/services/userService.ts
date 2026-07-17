import { http } from './http';
import type { CreateUserPayload, UmbralUserRow, UserRole } from '../types/user';

const BASE_URL = import.meta.env.VITE_USER_API_URL ?? 'http://localhost:5096/api';

/**
 * HU-23: cliente HTTP del UserService (puerto 5096). Todos los endpoints
 * requieren rol `admin` en el JWT. El http helper adjunta el Bearer
 * automáticamente, así que aquí solo definimos las rutas.
 */
export const userService = {
  list(): Promise<UmbralUserRow[]> {
    return http.get<UmbralUserRow[]>(`${BASE_URL}/users`);
  },

  create(payload: CreateUserPayload): Promise<{ id: string }> {
    return http.post<{ id: string }>(`${BASE_URL}/users`, payload);
  },

  changeRole(userId: string, newRole: UserRole): Promise<void> {
    return http.put<void>(`${BASE_URL}/users/${userId}/role`, { newRole });
  },

  disable(userId: string): Promise<void> {
    return http.put<void>(`${BASE_URL}/users/${userId}/disable`);
  },

  enable(userId: string): Promise<void> {
    return http.put<void>(`${BASE_URL}/users/${userId}/enable`);
  },

  sendTemporaryPassword(userId: string): Promise<void> {
    return http.post<void>(`${BASE_URL}/users/${userId}/send-temporary-password`);
  },
};
