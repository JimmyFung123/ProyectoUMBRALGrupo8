import { test, expect } from '@playwright/test';
import { seedMission, type MissionSeed } from '../mission-fixture';
import { startSessionWith, closeStartedSession } from '../helpers';

/**
 * Fase 3 — control de la sesión en vivo por el operador.
 *   - Pausa / Reanudar: el participante ve el overlay bloqueante y vuelve al juego.
 *   - Force-advance: el operador empuja al equipo a la siguiente etapa (0 puntos).
 *
 * Corre en serie (workers:1) porque comparte el stack con estado.
 */

function unique() {
  return Date.now().toString().slice(-6) + Math.floor(Math.random() * 100);
}

const Q1 = '¿Pregunta de la etapa uno?';
const Q2 = '¿Pregunta de la etapa dos?';

function oneStageMission(name: string): MissionSeed {
  return {
    name,
    difficulty: 'Easy',
    stages: [
      {
        title: 'Etapa 1',
        order: 1,
        question: Q1,
        options: [
          { text: 'Correcta', isCorrect: true },
          { text: 'Incorrecta', isCorrect: false },
        ],
      },
    ],
  };
}

function twoStageMission(name: string): MissionSeed {
  const seed = oneStageMission(name);
  seed.stages.push({
    title: 'Etapa 2',
    order: 2,
    question: Q2,
    options: [
      { text: 'Correcta dos', isCorrect: true },
      { text: 'Incorrecta dos', isCorrect: false },
    ],
  });
  return seed;
}

// ── Pausa / Reanudar ───────────────────────────────────────────────────────────

test('pausa y reanuda: el participante ve el overlay de pausa y vuelve al juego', async ({
  browser,
  request,
}) => {
  test.setTimeout(150_000);
  const id = unique();
  const seed = oneStageMission(`E2E Pausa ${id}`);
  await seedMission(request, seed);

  const s = await startSessionWith(browser, seed.name, `E2E Pausa ${id}`);
  try {
    await expect(s.pageA.getByText(Q1)).toBeVisible({ timeout: 30_000 });

    // El operador pausa → el participante recibe el overlay bloqueante.
    await s.operator.getByRole('button', { name: /Pausar/ }).click();
    await expect(s.pageA.getByText('Sesión pausada')).toBeVisible({ timeout: 20_000 });

    // El operador reanuda → el overlay desaparece y vuelve la trivia.
    await s.operator.getByRole('button', { name: /Reanudar/ }).click();
    await expect(s.pageA.getByText('Sesión pausada')).toBeHidden({ timeout: 20_000 });
    await expect(s.pageA.getByText(Q1)).toBeVisible({ timeout: 20_000 });
  } finally {
    await closeStartedSession(s);
  }
});

// ── Force-advance ──────────────────────────────────────────────────────────────

test('force-advance: el operador fuerza el avance del equipo a la siguiente etapa', async ({
  browser,
  request,
}) => {
  test.setTimeout(150_000);
  const id = unique();
  const seed = twoStageMission(`E2E Force ${id}`);
  await seedMission(request, seed);

  const s = await startSessionWith(browser, seed.name, `E2E Force ${id}`);
  try {
    // El equipo arranca en la etapa 1.
    await expect(s.pageA.getByText(Q1)).toBeVisible({ timeout: 30_000 });

    // El operador fuerza el avance (confirmación window.confirm → aceptar).
    s.operator.once('dialog', (d) => d.accept());
    await s.operator.getByRole('button', { name: /Forzar/ }).click();

    // En el panel de progreso el equipo pasa a "Etapa 2"…
    const progressTable = s.operator.locator('table').filter({ hasText: 'Acciones' });
    const teamRow = progressTable.getByRole('row').filter({ hasText: id });
    await expect(teamRow).toContainText('Etapa 2', { timeout: 30_000 });

    // …y el participante avanza a la etapa 2.
    await expect(s.pageA.getByText(Q2)).toBeVisible({ timeout: 30_000 });
  } finally {
    await closeStartedSession(s);
  }
});
