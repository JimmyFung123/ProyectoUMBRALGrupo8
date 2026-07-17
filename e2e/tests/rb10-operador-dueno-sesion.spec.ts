import { test, expect } from '@playwright/test';
import { OPERATOR_URL, OPERATOR_STORAGE, OPERATOR_B_STORAGE, ADMIN_STORAGE } from '../env';
import { seedMission, type MissionSeed } from '../mission-fixture';
import { createSessionAndOpenDashboard } from '../helpers';

/**
 * RB-10 — un operador solo administra/ve las sesiones que él mismo creó; el
 * Administrador las ve todas, en modo consulta (PR #33, front admin).
 *
 * No hay ruta navegable por sesión en la SPA (App.jsx no usa React Router,
 * `selectedSessionId` es solo estado local) — así que "entrar por URL directa"
 * no es un camino real hoy. La garantía observable por UI es que la sesión de
 * un operador simplemente no aparece en la lista de otro; el 403 a nivel HTTP
 * ya se cubre en UMBRAL_Back-end.IntegrationTests/Sessions/SessionOwnershipTests.cs.
 */

function unique() {
  return Date.now().toString().slice(-6) + Math.floor(Math.random() * 100);
}

function oneStageMission(name: string): MissionSeed {
  return {
    name,
    difficulty: 'Easy',
    stages: [
      {
        title: 'Etapa 1',
        order: 1,
        question: '¿Pregunta de RB-10?',
        options: [
          { text: 'Correcta', isCorrect: true },
          { text: 'Incorrecta', isCorrect: false },
        ],
      },
    ],
  };
}

test('RB-10: la sesión de un operador no aparece en la lista de otro, pero sí en la del Administrador', async ({
  browser,
  request,
}) => {
  test.setTimeout(90_000);
  const id = unique();
  const missionName = `E2E RB10 ${id}`;
  const sessionName = `Sesión RB10 ${id}`;
  await seedMission(request, oneStageMission(missionName));

  // Operador A crea la sesión.
  const aCtx = await browser.newContext({ storageState: OPERATOR_STORAGE });
  const a = await aCtx.newPage();
  try {
    await createSessionAndOpenDashboard(a, missionName, sessionName);
  } finally {
    await aCtx.close();
  }

  // Operador B no debe verla en su propia lista.
  const bCtx = await browser.newContext({ storageState: OPERATOR_B_STORAGE });
  const b = await bCtx.newPage();
  try {
    await b.goto(OPERATOR_URL);
    await expect(b.getByRole('heading', { name: 'Sesiones' })).toBeVisible({ timeout: 30_000 });
    await expect(b.getByRole('heading', { name: sessionName, exact: true })).toBeHidden();
  } finally {
    await bCtx.close();
  }

  // El Administrador la ve en modo consulta (pestaña "Sesiones" restaurada, RB-10).
  const adminCtx = await browser.newContext({ storageState: ADMIN_STORAGE });
  const admin = await adminCtx.newPage();
  try {
    await admin.goto(OPERATOR_URL);
    await expect(admin.getByRole('heading', { name: 'Misiones' })).toBeVisible({ timeout: 30_000 });
    await admin.getByRole('tab', { name: 'Sesiones' }).click();
    await expect(admin.getByRole('heading', { name: sessionName, exact: true })).toBeVisible({ timeout: 15_000 });
  } finally {
    await adminCtx.close();
  }
});
