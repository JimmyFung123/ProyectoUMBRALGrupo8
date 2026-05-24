const BASE_URL = import.meta.env.VITE_TEAM_API_URL ?? 'http://localhost:5095/api';

export async function createTeam(sessionId: string, teamName: string) {
  const res = await fetch(`${BASE_URL}/teams`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ sessionId, teamName }),
  });
  if (!res.ok) throw new Error('No se pudo crear el equipo');
  return res.json();
}

export async function joinTeam(inviteCode: string) {
  const res = await fetch(`${BASE_URL}/teams/${inviteCode.trim().toUpperCase()}/join`, {
    method: 'POST',
  });
  if (!res.ok) throw new Error('Código de equipo inválido o no encontrado');
  return res.json();
}

export async function getTeamInfo(teamId: string): Promise<{
  teamId: string;
  teamName: string;
  inviteCode: string;
  memberCount: number;
  currentStageOrder: number;
}> {
  const res = await fetch(`${BASE_URL}/teams/${teamId}`);
  if (!res.ok) throw new Error('No se pudo obtener el estado del equipo');
  return res.json();
}
