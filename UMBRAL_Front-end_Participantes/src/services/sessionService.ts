import type {
  ParticipantStage,
  QrValidationResult,
  ReleasedClues,
  TriviaAnswerResult,
} from '../types';

// Default: relative path → Vite proxy routes to SessionService.
// Works on localhost AND through HTTPS tunnels (cloudflared/ngrok).
// Override with VITE_SESSION_API_URL if pointing to a remote back-end.
const BASE_URL = import.meta.env.VITE_SESSION_API_URL ?? '/api';

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

export async function getReleasedClues(
  sessionId: string,
  teamId: string,
): Promise<ReleasedClues> {
  const res = await fetch(
    `${BASE_URL}/sessions/${sessionId}/teams/${teamId}/released-clues`,
  );
  if (!res.ok) throw new Error('No se pudieron obtener las pistas liberadas');
  return res.json();
}

export async function validateQrCode(
  sessionId: string,
  teamId: string,
  stageId: string,
  scannedCode: string,
): Promise<QrValidationResult> {
  const res = await fetch(`${BASE_URL}/sessions/${sessionId}/teams/${teamId}/validate-qr`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ stageId, scannedCode }),
  });
  if (!res.ok) throw new Error('No se pudo validar el código QR');
  return res.json();
}
