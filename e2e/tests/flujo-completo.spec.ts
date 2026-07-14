import { test, expect, type Page } from '@playwright/test';
import { OPERATOR_URL, PARTICIPANT_URL, OPERATOR_STORAGE } from '../env';
import { seedTriviaMission } from '../mission-fixture';

/**
 * Fase 1 — el "test estrella".
 *
 * Valida de punta a punta, con navegadores reales y SignalR real, el flujo
 * multi-actor completo de UMBRAL y, de paso, los dos fixes recientes:
 *   - Pistas por reintento (backend, commit 478755c): un fallo de trivia con
 *     intentos restantes bloquea la opción Y libera una pista al equipo.
 *   - Ranking "Finalizó" (front operador, commit 8e14bf4): al terminar la
 *     última etapa, el ranking en vivo del operador muestra "🏁 Finalizó".
 *
 * Reparto de identidades (el front operador separa la UI por rol de forma
 * EXCLUYENTE, ver env.ts):
 *   - Administrador → crea/activa la misión. Aquí la misión se SIEMBRA por API
 *     (controllers públicos) para que la precondición sea robusta; el foco del
 *     test es el juego, no la densa UI de administración.
 *   - Operador → crea la sesión, la arranca y observa el ranking en vivo.
 *   - Participantes A y B → anónimos, entran por código y juegan.
 *
 * Corre en serie (workers:1) porque comparte el stack con estado.
 */

// Etiquetas ASCII y distintas entre sí: evitan choques de substring al
// seleccionar botones por su nombre accesible.
const S1 = {
  title: 'Geografia',
  question: '¿Cual es la capital de España?',
  correct: 'Madrid',
  wrongA: 'Paris',
  wrongB: 'Roma',
};
const S2 = {
  title: 'Aritmetica',
  question: '¿Cuanto es 2 + 2?',
  correct: 'Cuatro',
  wrong: 'Tres',
};

test('flujo completo multi-actor: fallo→pista, acierto→avanza, ranking→Finalizó', async ({
  browser,
  request,
}) => {
  test.setTimeout(240_000);

  const stamp = Date.now().toString().slice(-6);
  const missionName = `E2E Mision ${stamp}`;
  const sessionName = `E2E Sesion ${stamp}`;
  const teamName = `Equipo ${stamp}`;

  // ── Precondición: sembrar la misión por API ───────────────────────────────
  await test.step('sembrar misión (2 etapas Trivia + pistas + regla "Intentos fallidos")', async () => {
    await seedTriviaMission(request, {
      name: missionName,
      difficulty: 'Easy',
      stages: [
        {
          title: S1.title,
          order: 1,
          question: S1.question,
          // maxAttempts=3 → el 1er fallo deja intentos y dispara bloqueo + pista.
          maxAttempts: 3,
          clues: ['Pista 1: es en la Península Ibérica.', 'Pista 2: empieza con M.'],
          options: [
            { text: S1.correct, isCorrect: true },
            { text: S1.wrongA, isCorrect: false },
            { text: S1.wrongB, isCorrect: false },
          ],
        },
        {
          title: S2.title,
          order: 2, // última etapa (sin regla de intentos: acierto avanza directo)
          question: S2.question,
          options: [
            { text: S2.correct, isCorrect: true },
            { text: S2.wrong, isCorrect: false },
          ],
        },
      ],
    });
  });

  // ── Contextos: operador (sesión guardada) + 2 participantes anónimos ───────
  const operatorCtx = await browser.newContext({ storageState: OPERATOR_STORAGE });
  const participantACtx = await browser.newContext();
  const participantBCtx = await browser.newContext();
  const operator = await operatorCtx.newPage();
  const pageA = await participantACtx.newPage();
  const pageB = await participantBCtx.newPage();

  try {
    // ── El operador crea la sesión y obtiene el código de acceso ─────────────
    let accessCode = '';
    await test.step('operador crea la sesión y lee el código de acceso', async () => {
      await operator.goto(OPERATOR_URL);
      // El operador aterriza en la pestaña Sesiones (única de su rol).
      await expect(operator.getByRole('heading', { name: 'Sesiones' })).toBeVisible({ timeout: 30_000 });

      await operator.locator('#session-mission').selectOption({ label: missionName });
      await operator.locator('#session-name').fill(sessionName);
      const createBtn = operator.getByRole('button', { name: /Crear sesión/ });

      // Reintento: SessionService valida la misión contra su réplica
      // MissionLookup, que se sincroniza por eventos (consistencia eventual).
      await expect(async () => {
        if (!(await operator.locator('#session-mission').inputValue())) {
          await operator.locator('#session-mission').selectOption({ label: missionName });
          await operator.locator('#session-name').fill(sessionName);
        }
        await createBtn.click();
        await expect(
          operator.getByRole('heading', { name: sessionName, exact: true }),
        ).toBeVisible({ timeout: 3_000 });
      }).toPass({ timeout: 40_000, intervals: [1_000, 1_500, 2_000, 3_000] });

      // Abrir el tablero de la sesión recién creada.
      const sessionCard = operator
        .locator('div')
        .filter({ has: operator.getByRole('heading', { name: sessionName, exact: true }) })
        .filter({ has: operator.getByRole('button', { name: 'Ver detalle' }) })
        .last();
      await sessionCard.getByRole('button', { name: 'Ver detalle' }).click();

      // El código vive bajo la etiqueta "Código para participantes".
      const codeValue = operator
        .getByText('Código para participantes')
        .locator('xpath=following-sibling::div[1]');
      await expect(codeValue).toBeVisible({ timeout: 20_000 });
      accessCode = (await codeValue.textContent())?.trim() ?? '';
      expect(accessCode).toMatch(/^[A-Z0-9]{6}$/);
    });

    // ── Participante A entra y crea el equipo ────────────────────────────────
    let inviteCode = '';
    await test.step('participante A entra por código y crea el equipo', async () => {
      await joinSession(pageA, accessCode);
      await enterNickname(pageA, 'Ana');
      await pageA.getByRole('button', { name: /Crear equipo/ }).click();
      await pageA.getByPlaceholder('Ej: Los Campeones').fill(teamName);
      await pageA.getByRole('button', { name: /Crear equipo/ }).click();

      // Sala de espera del líder: leer el código de invitación del equipo.
      const codeSpan = pageA
        .getByRole('button', { name: /Copiar/i })
        .locator('xpath=preceding-sibling::span[1]');
      await expect(codeSpan).toBeVisible({ timeout: 15_000 });
      inviteCode = (await codeSpan.textContent())?.trim() ?? '';
      expect(inviteCode.length).toBeGreaterThanOrEqual(3);
    });

    // ── Participante B entra y se une al mismo equipo (RB-18: ≥2) ─────────────
    await test.step('participante B entra y se une al equipo (llega a 2 miembros)', async () => {
      await joinSession(pageB, accessCode);
      await enterNickname(pageB, 'Beto');
      await pageB.getByRole('button', { name: /Unirme a un equipo/ }).click();
      await pageB.getByPlaceholder('Ej: XY42').fill(inviteCode);
      await pageB.getByRole('button', { name: /Unirme al equipo/ }).click();

      // Con 2 integrantes, la sala muestra "¡Equipo listo!".
      await expect(pageB.getByText('¡Equipo listo!')).toBeVisible({ timeout: 15_000 });
    });

    // ── El operador arranca la sesión ────────────────────────────────────────
    await test.step('operador arranca la sesión', async () => {
      const startBtn = operator.getByRole('button', { name: /Iniciar/ });
      // El tablero refresca por poll (10s) hasta ver el equipo inscrito.
      await expect(startBtn).toBeEnabled({ timeout: 30_000 });
      await startBtn.click();
      // La sesión pasa a "En progreso" (el botón Iniciar desaparece).
      await expect(operator.getByRole('button', { name: /Finalizar/ })).toBeVisible({ timeout: 20_000 });
    });

    // ── Etapa 1: fallo → pista por reintento; acierto → avanza ────────────────
    await test.step('A responde etapa 1: fallo libera pista, acierto avanza', async () => {
      // Los participantes pasan solos de la sala de espera al juego.
      await expect(pageA.getByText(S1.question)).toBeVisible({ timeout: 30_000 });

      // Fallo con intentos restantes.
      await pageA.getByRole('button', { name: S1.wrongA }).click();
      await pageA.getByRole('button', { name: /Confirmar respuesta/ }).click();

      // FIX pistas-por-reintento: la opción queda bloqueada Y aparece una pista.
      await expect(pageA.getByRole('button', { name: new RegExp(S1.wrongA) })).toBeDisabled({ timeout: 15_000 });
      await expect(pageA.getByText(/Pistas recibidas/)).toBeVisible({ timeout: 25_000 });

      // Acierto → avanza y suma puntos.
      await pageA.getByRole('button', { name: S1.correct }).click();
      await pageA.getByRole('button', { name: /Confirmar respuesta/ }).click();
      await expect(pageA.getByText('¡Correcto!')).toBeVisible({ timeout: 15_000 });
    });

    // ── El ranking del operador refleja el avance a la etapa 2 ────────────────
    const rankingTable = operator.locator('table').filter({ hasText: 'resoluci' });
    const teamRow = rankingTable.getByRole('row').filter({ hasText: teamName });
    await test.step('ranking en vivo del operador: el equipo avanza a "Etapa 2"', async () => {
      await expect(teamRow).toContainText('Etapa 2', { timeout: 30_000 });
    });

    // ── Etapa 2 (última): acierto → completa la misión ───────────────────────
    await test.step('A responde etapa 2 (última) y completa la misión', async () => {
      await expect(pageA.getByText(S2.question)).toBeVisible({ timeout: 30_000 });
      await pageA.getByRole('button', { name: S2.correct }).click();
      await pageA.getByRole('button', { name: /Confirmar respuesta/ }).click();
      await expect(pageA.getByText('¡Completaste la misión!')).toBeVisible({ timeout: 20_000 });
    });

    // ── FIX ranking "Finalizó": al terminar la última etapa ──────────────────
    await test.step('ranking en vivo del operador: el equipo muestra "🏁 Finalizó"', async () => {
      await expect(teamRow).toContainText('Finalizó', { timeout: 30_000 });
    });
  } finally {
    await operatorCtx.close();
    await participantACtx.close();
    await participantBCtx.close();
  }
});

// ── Helpers de participante ───────────────────────────────────────────────────

async function joinSession(page: Page, code: string) {
  await page.goto(PARTICIPANT_URL);
  await page.getByPlaceholder('Ej: ABC123').fill(code);
  await page.getByRole('button', { name: /Entrar a la sesión/ }).click();
}

async function enterNickname(page: Page, nickname: string) {
  await page.getByPlaceholder('Tu apodo temporal').fill(nickname);
}
