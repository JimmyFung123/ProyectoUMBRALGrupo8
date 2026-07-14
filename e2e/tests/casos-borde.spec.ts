import { test, expect } from '@playwright/test';
import { OPERATOR_STORAGE } from '../env';
import { seedMission, type MissionSeed } from '../mission-fixture';
import {
  createTeamAsLeader,
  createSessionAndOpenDashboard,
  startSessionWith,
  closeStartedSession,
} from '../helpers';

/**
 * Fase 2 — casos borde.
 *
 * Complementan al "test estrella" (flujo-completo.spec.ts) con escenarios que
 * ejercitan reglas y acciones puntuales del operador:
 *   - RB-18: una sesión no arranca si algún equipo tiene menos de 2 miembros.
 *   - Broadcast: el operador manda un mensaje en vivo y el participante lo recibe.
 *   - Penalización: el operador resta puntos y el participante ve la sanción.
 *   - Treasure Hunt: el participante valida el código QR (entrada manual) y avanza.
 *
 * Corre en serie (workers:1) porque comparte el stack con estado.
 */

function unique() {
  return Date.now().toString().slice(-6) + Math.floor(Math.random() * 100);
}

/** Misión mínima de una etapa Trivia, suficiente para llegar al juego. */
function triviaMission(name: string): MissionSeed {
  return {
    name,
    difficulty: 'Easy',
    stages: [
      {
        title: 'Pregunta',
        order: 1,
        question: '¿Cuanto es 3 + 4?',
        options: [
          { text: 'Siete', isCorrect: true },
          { text: 'Ocho', isCorrect: false },
        ],
      },
    ],
  };
}

// ── RB-18: equipo incompleto no arranca ───────────────────────────────────────

test('RB-18: la sesión NO arranca si un equipo tiene menos de 2 miembros', async ({
  browser,
  request,
}) => {
  test.setTimeout(120_000);
  const id = unique();
  const missionName = `E2E RB18 ${id}`;
  await seedMission(request, triviaMission(missionName));

  const operatorCtx = await browser.newContext({ storageState: OPERATOR_STORAGE });
  const aCtx = await browser.newContext();
  const operator = await operatorCtx.newPage();
  const pageA = await aCtx.newPage();

  try {
    const code = await createSessionAndOpenDashboard(operator, missionName, `E2E RB18 ${id}`);

    // Solo UN participante crea equipo (queda con 1 miembro).
    await createTeamAsLeader(pageA, code, 'Ana', `Equipo ${id}`);

    // El botón se habilita (hay 1 equipo inscrito) pero el backend rechaza el
    // arranque por RB-18 (≥2 miembros por equipo).
    const startBtn = operator.getByRole('button', { name: /Iniciar/ });
    await expect(startBtn).toBeEnabled({ timeout: 30_000 });
    await startBtn.click();

    // La sesión sigue Pendiente: el operador ve un error y NO aparece "Finalizar".
    await expect(operator.getByRole('alert')).toBeVisible({ timeout: 15_000 });
    await expect(operator.getByRole('button', { name: /Iniciar/ })).toBeVisible();
    await expect(operator.getByRole('button', { name: /Finalizar/ })).toHaveCount(0);

    // El participante nunca sale de la sala de espera.
    await expect(pageA.getByText('Sala de espera')).toBeVisible();
    await expect(pageA.getByText('¿Cuanto es 3 + 4?')).toHaveCount(0);
  } finally {
    await operatorCtx.close();
    await aCtx.close();
  }
});

// ── Broadcast del operador ─────────────────────────────────────────────────────

test('broadcast: el operador envía un mensaje en vivo y el participante lo recibe', async ({
  browser,
  request,
}) => {
  test.setTimeout(150_000);
  const id = unique();
  const seed = triviaMission(`E2E Broadcast ${id}`);
  await seedMission(request, seed);

  const s = await startSessionWith(browser, seed.name,`E2E Broadcast ${id}`);
  try {
    // A ya está en el juego y con la conexión en vivo (SignalR) lista.
    await expect(s.pageA.getByText('¿Cuanto es 3 + 4?')).toBeVisible({ timeout: 30_000 });
    await expect(s.pageA.getByText('En vivo')).toBeVisible({ timeout: 15_000 });

    const message = `Faltan 5 minutos ${id}`;
    await s.operator.getByRole('button', { name: /Enviar mensaje/ }).click();
    await s.operator.locator('#broadcast-msg').fill(message);
    await s.operator.getByRole('dialog').getByRole('button', { name: /Enviar/ }).click();

    // El mensaje aparece como notificación en la pantalla del participante.
    await expect(s.pageA.getByText(message)).toBeVisible({ timeout: 15_000 });
  } finally {
    await closeStartedSession(s);
  }
});

// ── Penalización del operador ──────────────────────────────────────────────────

test('penalización: el operador resta puntos y el participante ve la sanción', async ({
  browser,
  request,
}) => {
  test.setTimeout(150_000);
  const id = unique();
  const seed = triviaMission(`E2E Penal ${id}`);
  await seedMission(request, seed);

  const s = await startSessionWith(browser, seed.name,`E2E Penal ${id}`);
  try {
    await expect(s.pageA.getByText('¿Cuanto es 3 + 4?')).toBeVisible({ timeout: 30_000 });
    await expect(s.pageA.getByText('En vivo')).toBeVisible({ timeout: 15_000 });

    // Operador penaliza al equipo (solo posible con la sesión en progreso).
    const reason = `Uso de celular ${id}`;
    await s.operator.getByRole('button', { name: /Penalizar/ }).click();
    await s.operator.locator('#penalty-points').fill('5');
    await s.operator.locator('#penalty-reason').fill(reason);
    await s.operator.getByRole('dialog').getByRole('button', { name: /Aplicar penalización/ }).click();

    // El operador confirma la sanción y el participante ve el motivo en un toast.
    await expect(s.operator.getByText(/Penalización aplicada/)).toBeVisible({ timeout: 15_000 });
    await expect(s.pageA.getByText(reason)).toBeVisible({ timeout: 15_000 });
  } finally {
    await closeStartedSession(s);
  }
});

// ── Treasure Hunt / validación de QR (entrada manual) ──────────────────────────

test('treasure hunt: el participante valida el código QR y completa la etapa', async ({
  browser,
  request,
}) => {
  test.setTimeout(150_000);
  const id = unique();
  const missionName = `E2E QR ${id}`;
  const qrCode = `TESORO-${id}`;
  const seed: MissionSeed = {
    name: missionName,
    difficulty: 'Easy',
    stages: [
      {
        kind: 'treasure',
        title: 'Encontrá el tesoro',
        order: 1,
        latitude: 10.4866,
        longitude: -66.8543,
        qrCode,
      },
    ],
  };
  await seedMission(request, seed);

  const s = await startSessionWith(browser, seed.name,`E2E QR ${id}`);
  try {
    // A llega a la pantalla de búsqueda del tesoro.
    const manualBtn = s.pageA.getByRole('button', { name: /Ingresar código manualmente/ });
    await expect(manualBtn).toBeVisible({ timeout: 30_000 });
    await manualBtn.click();

    // Ingresa el código correcto → valida y, al ser la última etapa, completa.
    await s.pageA.getByPlaceholder('Código QR').fill(qrCode);
    await s.pageA.getByRole('button', { name: /Validar código/ }).click();

    await expect(s.pageA.getByText('¡Completaste la misión!')).toBeVisible({ timeout: 20_000 });
  } finally {
    await closeStartedSession(s);
  }
});
