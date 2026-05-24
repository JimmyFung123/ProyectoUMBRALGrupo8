import type { ParticipantStage, TriviaAnswerResult } from '../types';

const BASE_URL = import.meta.env.VITE_SESSION_API_URL ?? 'http://localhost:5092/api';

export async function getSessionByCode(code: string) {
  const res = await fetch(`${BASE_URL}/sessions/by-code/${code.trim().toUpperCase()}`);
  if (!res.ok) throw new Error('Sesión no encontrada');
  return res.json();
}

export async function getParticipantStage(
  sessionId: string,
  teamId: string,
): Promise<ParticipantStage> {
  const res = await fetch(`${BASE_URL}/sessions/${sessionId}/participant-stage/${teamId}`);
  if (!res.ok) throw new Error('No se pudo obtener la etapa actual');
  return res.json();
}

export async function submitTriviaAnswer(
  sessionId: string,
  teamId: string,
  stageId: string,
  optionId: string,
): Promise<TriviaAnswerResult> {
  const res = await fetch(`${BASE_URL}/sessions/${sessionId}/teams/${teamId}/answer-trivia`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ stageId, optionId }),
  });
  if (!res.ok) throw new Error('No se pudo registrar la respuesta');
  return res.json();
}
