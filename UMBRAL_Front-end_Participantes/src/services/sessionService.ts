const BASE_URL = import.meta.env.VITE_SESSION_API_URL ?? 'http://localhost:5092/api';

export async function getSessionByCode(code: string) {
  const res = await fetch(`${BASE_URL}/sessions/by-code/${code.trim().toUpperCase()}`);
  if (!res.ok) throw new Error('Sesión no encontrada');
  return res.json();
}
