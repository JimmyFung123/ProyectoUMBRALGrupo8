import { expect, type Page } from '@playwright/test';
import { OPERATOR_URL, PARTICIPANT_URL } from './env';

/**
 * Helpers reutilizables para los flujos que se repiten en los tests E2E:
 * ingreso de participantes (por código) y montaje de la sesión por el operador.
 * Los selectores son los mismos ya validados por el "test estrella" de Fase 1.
 */

// ── Participante ──────────────────────────────────────────────────────────────

export async function joinSession(page: Page, code: string): Promise<void> {
  await page.goto(PARTICIPANT_URL);
  await page.getByPlaceholder('Ej: ABC123').fill(code);
  await page.getByRole('button', { name: /Entrar a la sesión/ }).click();
  await page.getByPlaceholder('Tu apodo temporal').waitFor({ timeout: 15_000 });
}

/** Entra a la sesión, elige apodo y CREA un equipo. Devuelve el código de invitación. */
export async function createTeamAsLeader(
  page: Page,
  code: string,
  nickname: string,
  teamName: string,
): Promise<string> {
  await joinSession(page, code);
  await page.getByPlaceholder('Tu apodo temporal').fill(nickname);
  await page.getByRole('button', { name: /Crear equipo/ }).click();
  await page.getByPlaceholder('Ej: Los Campeones').fill(teamName);
  await page.getByRole('button', { name: /Crear equipo/ }).click();

  const codeSpan = page
    .getByRole('button', { name: /Copiar/i })
    .locator('xpath=preceding-sibling::span[1]');
  await expect(codeSpan).toBeVisible({ timeout: 15_000 });
  const inviteCode = (await codeSpan.textContent())?.trim() ?? '';
  expect(inviteCode.length).toBeGreaterThanOrEqual(3);
  return inviteCode;
}

/** Entra a la sesión, elige apodo y SE UNE a un equipo por su código de invitación. */
export async function joinTeamAsMember(
  page: Page,
  code: string,
  nickname: string,
  inviteCode: string,
): Promise<void> {
  await joinSession(page, code);
  await page.getByPlaceholder('Tu apodo temporal').fill(nickname);
  await page.getByRole('button', { name: /Unirme a un equipo/ }).click();
  await page.getByPlaceholder('Ej: XY42').fill(inviteCode);
  await page.getByRole('button', { name: /Unirme al equipo/ }).click();
  await expect(page.getByText('¡Equipo listo!')).toBeVisible({ timeout: 15_000 });
}

// ── Operador ──────────────────────────────────────────────────────────────────

/**
 * Crea la sesión para `missionName`, abre su tablero y devuelve el código de
 * acceso (6 chars). Reintenta la creación porque SessionService valida la
 * misión contra su réplica MissionLookup (consistencia eventual).
 */
export async function createSessionAndOpenDashboard(
  operator: Page,
  missionName: string,
  sessionName: string,
): Promise<string> {
  await operator.goto(OPERATOR_URL);
  await expect(operator.getByRole('heading', { name: 'Sesiones' })).toBeVisible({ timeout: 30_000 });

  await operator.locator('#session-mission').selectOption({ label: missionName });
  await operator.locator('#session-name').fill(sessionName);
  const createBtn = operator.getByRole('button', { name: /Crear sesión/ });

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

  const sessionCard = operator
    .locator('div')
    .filter({ has: operator.getByRole('heading', { name: sessionName, exact: true }) })
    .filter({ has: operator.getByRole('button', { name: 'Ver detalle' }) })
    .last();
  await sessionCard.getByRole('button', { name: 'Ver detalle' }).click();

  const codeValue = operator
    .getByText('Código para participantes')
    .locator('xpath=following-sibling::div[1]');
  await expect(codeValue).toBeVisible({ timeout: 20_000 });
  const accessCode = (await codeValue.textContent())?.trim() ?? '';
  expect(accessCode).toMatch(/^[A-Z0-9]{6}$/);
  return accessCode;
}

/** Arranca la sesión desde el tablero (espera a que el equipo esté inscrito). */
export async function startSession(operator: Page): Promise<void> {
  const startBtn = operator.getByRole('button', { name: /Iniciar/ });
  await expect(startBtn).toBeEnabled({ timeout: 30_000 });
  await startBtn.click();
  await expect(operator.getByRole('button', { name: /Finalizar/ })).toBeVisible({ timeout: 20_000 });
}
