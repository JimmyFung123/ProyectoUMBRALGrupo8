import type { APIRequestContext } from '@playwright/test';
import { MISSION_API, STAGE_API, CLUE_API } from './env';

/**
 * Siembra por API la misión que consumen los tests. Los controllers de
 * Mission/Stage/Clue son públicos (sin [Authorize]), así que no hace falta
 * token. Se hace por API (y no por la UI de administración) para que la
 * precondición sea robusta y rápida; el valor de los tests está en ejercitar
 * por UI el juego multi-actor y los casos borde.
 *
 * Ojo con la consistencia eventual: SessionService valida la misión contra su
 * réplica MissionLookup (sincronizada por eventos MassTransit). Por eso la
 * creación de la SESIÓN (que sí va por la UI del operador) se hace con reintento.
 */

export interface TriviaOptionSeed {
  text: string;
  isCorrect: boolean;
}

interface BaseStageSeed {
  title: string;
  order: number;
  baseScore?: number;
  /** Regla "Intentos fallidos": > 0 activa el bloqueo de opción + pista por reintento. */
  maxAttempts?: number;
  /** Contenido de las pistas de la etapa (en orden). */
  clues?: string[];
}

export interface TriviaStageSeed extends BaseStageSeed {
  kind?: 'trivia';
  question: string;
  options: TriviaOptionSeed[];
}

export interface TreasureStageSeed extends BaseStageSeed {
  kind: 'treasure';
  latitude: number;
  longitude: number;
  qrCode: string;
}

export type StageSeed = TriviaStageSeed | TreasureStageSeed;

export interface MissionSeed {
  name: string;
  difficulty?: 'Easy' | 'Medium' | 'Hard';
  stages: StageSeed[];
}

function isTreasure(stage: StageSeed): stage is TreasureStageSeed {
  return (stage as TreasureStageSeed).kind === 'treasure' || 'qrCode' in stage;
}

/** Extrae el id de una respuesta que puede venir como string crudo o { id }. */
function readId(body: unknown): string {
  if (typeof body === 'string') return body;
  if (body && typeof body === 'object' && 'id' in body) {
    return String((body as { id: unknown }).id);
  }
  throw new Error(`Respuesta sin id reconocible: ${JSON.stringify(body)}`);
}

async function postJson(
  request: APIRequestContext,
  url: string,
  data: unknown,
): Promise<unknown> {
  const res = await request.post(url, { data });
  if (!res.ok()) {
    throw new Error(`POST ${url} → ${res.status()}: ${await res.text()}`);
  }
  return res.json();
}

/**
 * POST con reintento ante 404 por consistencia eventual: ClueService mantiene
 * su propia réplica StageLookup, sincronizada por eventos desde StageService.
 * Justo tras crear la etapa, la pista puede llegar antes que el evento y el
 * back responde 404 "Stage not found in lookup" — reintentamos hasta que llegue.
 */
async function postJsonWithRetry(
  request: APIRequestContext,
  url: string,
  data: unknown,
  timeoutMs = 15_000,
): Promise<unknown> {
  const deadline = Date.now() + timeoutMs;
  let lastText = '';
  for (;;) {
    const res = await request.post(url, { data });
    if (res.ok()) return res.json();
    lastText = await res.text();
    if (res.status() !== 404 || Date.now() > deadline) {
      throw new Error(`POST ${url} → ${res.status()}: ${lastText}`);
    }
    await new Promise((r) => setTimeout(r, 500));
  }
}

async function addStage(
  request: APIRequestContext,
  missionId: string,
  stage: StageSeed,
): Promise<void> {
  const common = {
    missionId,
    title: stage.title,
    order: stage.order,
    baseScore: stage.baseScore ?? 100,
  };
  const body = isTreasure(stage)
    ? { ...common, type: 'TreasureHunt', latitude: stage.latitude, longitude: stage.longitude, qrCode: stage.qrCode }
    : { ...common, type: 'Trivia', question: stage.question, options: stage.options };

  const stageBody = await postJson(request, `${STAGE_API}/stages`, body);
  const stageId = readId(stageBody);

  if (stage.maxAttempts && stage.maxAttempts > 0) {
    const res = await request.patch(`${STAGE_API}/stages/${stageId}/auto-release`, {
      data: { timeMinutes: null, maxAttempts: stage.maxAttempts },
    });
    if (!res.ok()) {
      throw new Error(`PATCH auto-release → ${res.status()}: ${await res.text()}`);
    }
  }

  const clues = stage.clues ?? [];
  for (let i = 0; i < clues.length; i++) {
    await postJsonWithRetry(request, `${CLUE_API}/clues`, {
      stageId,
      order: i + 1,
      content: clues[i],
    });
  }
}

/** Crea misión + etapas (trivia y/o treasure) + pistas + reglas y la deja ACTIVA. */
export async function seedMission(
  request: APIRequestContext,
  seed: MissionSeed,
): Promise<string> {
  const missionBody = await postJson(request, `${MISSION_API}/missions`, {
    name: seed.name,
    description: 'Misión generada por E2E.',
    difficulty: seed.difficulty ?? 'Easy',
    maxDuration: 30,
  });
  const missionId = readId(missionBody);

  for (const stage of seed.stages) {
    await addStage(request, missionId, stage);
  }

  // Activar: habilita generar sesiones y dispara el evento que sincroniza la
  // réplica MissionLookup de SessionService.
  const activate = await request.patch(`${MISSION_API}/missions/${missionId}/status`, {
    data: { activate: true },
  });
  if (!activate.ok()) {
    throw new Error(`PATCH status activate → ${activate.status()}: ${await activate.text()}`);
  }

  return missionId;
}

/** Alias retrocompatible usado por el flujo completo (misiones solo de trivia). */
export const seedTriviaMission = seedMission;
